#!/usr/bin/env bash
# Hygiene checks for the prod stack, no VM needed:
#   1. docker-compose.prod.yml wiring (healthchecks, log rotation, ordering, PEDro off)
#   2. the healthcheck commands themselves, run inside the real images
#   3. host/disk-alert.sh against a stub ping server
# Needs a running Docker daemon. Usage: docker/test-hygiene.sh
set -euo pipefail
export MSYS_NO_PATHCONV=1 # Git Bash on Windows would otherwise rewrite /usr/... style args
cd "$(dirname "$0")"

NET=ptb-h-net
fail() { echo "FAIL: $*" >&2; exit 1; }
cleanup() {
  docker rm -f ptb-h-hb ptb-h-proxy ptb-h-stub ptb-h-api ptb-h-api404 ptb-h-web ptb-h-kc ptb-h-db >/dev/null 2>&1 || true
  docker network rm $NET >/dev/null 2>&1 || true
}
trap cleanup EXIT
cleanup

printf 'FROM alpine:3.21\nRUN apk add --no-cache jq\n' | docker build -q -t ptb-h-jq - >/dev/null
jq_() { docker run --rm -i ptb-h-jq jq "$@"; }
# Retry a command for up to $1 seconds; a broken setup fails the test instead of hanging it.
wait_for() { local secs=$1; shift; for _ in $(seq "$secs"); do "$@" >/dev/null 2>&1 && return 0; sleep 1; done; return 1; }

COMPOSE_JSON=$(GHCR_OWNER=x TAG=1 PUBLIC_ORIGIN=https://x POSTGRES_DB=d POSTGRES_USER=u POSTGRES_PASSWORD=p \
  KEYCLOAK_ADMIN_PASSWORD=k KEYCLOAK_SMTP_USER=u KEYCLOAK_SMTP_PASSWORD=p KEYCLOAK_ADMIN_CLIENT_SECRET=s EMAIL_API_KEY=e EMAIL_FROM_ADDRESS=f@x.org \
  docker compose -f docker-compose.prod.yml config --format json 2>/dev/null)

echo "== compose wiring =="
offenders=$(jq_ -r '.services | to_entries[]
  | select((.value.logging.options["max-size"] != "10m") or (.value.logging.options["max-file"] != "3")) | .key' <<<"$COMPOSE_JSON")
[ -z "$offenders" ] || fail "services without 10m x 3 log rotation: $offenders"

offenders=$(jq_ -r '. as $c | ["api","web","keycloak","db"][] | select($c.services[.].healthcheck == null)' <<<"$COMPOSE_JSON")
[ -z "$offenders" ] || fail "services without a healthcheck: $offenders"

offenders=$(jq_ -r '. as $c | .services | to_entries[] | .key as $svc | (.value.depends_on // {}) | to_entries[]
  | select($c.services[.key].healthcheck != null and .value.condition != "service_healthy") | "\($svc) -> \(.key)"' <<<"$COMPOSE_JSON")
[ -z "$offenders" ] || fail "dependencies that do not wait for service_healthy: $offenders"

[ "$(jq_ -r '.services.api.environment.Sources__Pedro__Enabled' <<<"$COMPOSE_JSON")" = "false" ] \
  || fail "PEDro must be pinned off in the api environment"

echo "== keycloak hardening: realm file and admin-console exposure =="
REALM=keycloak/realm-export.prod.json
realm_jq() { jq_ -r "$1" < $REALM; }
[ "$(realm_jq '[.clients[].clientId] as $c | [.users[].serviceAccountClientId // empty | select(IN($c[]) | not)] | length')" = 0 ]   || fail "service-account user names a client that is not defined in the realm"
[ "$(realm_jq '.users[] | select(.serviceAccountClientId == "pubtracker-api-admin").serviceAccountClientId')" = "pubtracker-api-admin" ] || fail "service-account user must be attached to pubtracker-api-admin"
[ "$(realm_jq '.users[] | select(.serviceAccountClientId == "pubtracker-api-admin").username')" = "service-account-pubtracker-api-admin" ] || fail "service-account username must be service-account-<clientId>"
[ "$(realm_jq '.users[] | select(.serviceAccountClientId == "pubtracker-api-admin").clientRoles["realm-management"] | sort | join(",")')" = "manage-users,view-users" ] || fail "service account lacks the user-management roles"
[ "$(realm_jq '.verifyEmail')" = "true" ] || fail "email verification must be required"
[ "$(realm_jq '.bruteForceProtected')" = "true" ] || fail "brute-force protection must be enabled"
[ "$(realm_jq '.smtpServer.host')" = "smtp-relay.brevo.com" ] || fail "realm SMTP must point at Brevo"
realm_jq '.clients[] | select(.clientId=="pubtracker-api-admin") | .description' | grep -qi "dev secret" && fail "stale 'Dev secret' description"
[ "$(jq_ -r '.services.keycloak.ports[0]' <<<"$COMPOSE_JSON" | grep -c '127.0.0.1')" = 1 ] || fail "keycloak must publish only on the VM loopback"
[ "$(jq_ -r '.services.keycloak.environment.KC_HOSTNAME_ADMIN' <<<"$COMPOSE_JSON")" != "null" ] || fail "keycloak needs KC_HOSTNAME_ADMIN for the tunnel"

# Caddy in front of one stub that echoes the request headers as its body, reachable as web, api and keycloak.
docker network create $NET >/dev/null
docker run -d --name ptb-h-stub --network $NET --network-alias web --network-alias api --network-alias keycloak python:3-alpine python -u -c '
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
class H(BaseHTTPRequestHandler):
    def do_GET(self):
        body = str(self.headers).encode()
        self.send_response(200); self.send_header("Content-Length", str(len(body))); self.end_headers(); self.wfile.write(body)
for port in (80, 8080):
    threading.Thread(target=HTTPServer(("", port), H).serve_forever, daemon=True).start()
threading.Event().wait()' >/dev/null
docker run -d --name ptb-h-proxy --network $NET -v "$PWD/caddy/Caddyfile.prod:/etc/caddy/Caddyfile:ro" caddy:2-alpine >/dev/null
# Prints the response headers, a blank line, then the body.
proxy_get() { docker run --rm --network $NET curlimages/curl -s -i --max-time 5 "${@:2}" "http://ptb-h-proxy:80$1" || true; }
wait_for 20 docker exec ptb-h-proxy wget -q -O /dev/null http://127.0.0.1:80/ || fail "caddy never served the stub web upstream"
status_of() { proxy_get "$@" | awk '/^ *HTTP\//{print $2}' | tail -1; }
for blocked in /auth/admin/ /auth/admin/master/console/ /auth/admin/realms /auth/realms/master /auth/realms/master/protocol/openid-connect/auth; do
  [ "$(status_of $blocked)" = 404 ] || fail "$blocked must be refused by the proxy"
done
[ "$(status_of /auth/realms/science-alerts-saas)" = 200 ] || fail "the app realm must still be proxied to keycloak"
for path in / /api/health /auth/realms/science-alerts-saas /auth/admin/; do
  headers=$(proxy_get $path | tr 'A-Z' 'a-z')
  for h in 'strict-transport-security: max-age=' 'x-content-type-options: nosniff' 'referrer-policy: '; do
    grep -q "$h" <<<"$headers" || fail "$path is missing the header '$h'"
  done
done
proxy_get / | grep -qi "content-security-policy: default-src 'self'" || fail "the SPA must be served with a CSP"
kc_page=$(proxy_get /auth/realms/science-alerts-saas); grep -q ' 200 ' <<<"$kc_page" || fail "keycloak stub page not served"
grep -qi "content-security-policy" <<<"$kc_page" && fail "keycloak pages must keep their own CSP, not ours"
# The client address set by Cloudflare/cloudflared reaches the api: Caddy trusts private peers.
proxy_get /api/Users/me --header 'X-Forwarded-For: 203.0.113.9' --header 'X-Forwarded-Proto: https' | grep -i '^x-forwarded-for:' | grep -q '^X-Forwarded-For: 203.0.113.9'   || fail "the api must receive the client address from X-Forwarded-For"
proxy_get /api/Users/me --header 'X-Forwarded-Proto: https' | grep -qi '^x-forwarded-proto: https' || fail "the api must receive X-Forwarded-Proto: https"

echo "== disk alert pings success under the threshold and /fail at or over it =="
docker run -d --name ptb-h-hb --network $NET python:3-alpine \
  sh -c 'mkdir -p /srv/ping && echo ok > /srv/ping/index.html && echo ok > /srv/ping/fail && cd /srv && python -u -m http.server 8080' >/dev/null
printf 'FROM alpine:3.21\nRUN apk add --no-cache curl\nCOPY host/disk-alert.sh /disk-alert.sh\n' \
  | docker build -q -t ptb-h-alert -f - . >/dev/null
hits() { docker logs ptb-h-hb 2>&1 | grep -c "$1" || true; }
alert() { docker run --rm --network $NET -e DISK_ALERT_PING_URL=http://ptb-h-hb:8080/ping "$@" ptb-h-alert sh /disk-alert.sh; }
wait_for 30 alert -e DISK_ALERT_THRESHOLD=101 || fail "disk-alert.sh failed under the threshold"
[ "$(hits 'GET /ping HTTP')" -ge 1 ] || fail "no success ping when usage is under the threshold"
[ "$(hits 'GET /ping/fail')" = 0 ] || fail "failure ping sent when usage is under the threshold"
alert -e DISK_ALERT_THRESHOLD=1 >/dev/null 2>&1 || fail "disk-alert.sh failed over the threshold"
[ "$(hits 'GET /ping/fail')" = 1 ] || fail "expected one /fail ping when usage is at or over the threshold"
if docker run --rm ptb-h-alert sh /disk-alert.sh >/dev/null 2>&1; then fail "disk-alert.sh must refuse to run without DISK_ALERT_PING_URL"; fi

echo "== healthcheck commands work inside the real images =="
# Each command is read from the compose file, so the test runs exactly what production runs.
cmd_of() { # service -> its exec-form healthcheck args, one per line
  [ "$(jq_ -r ".services.$1.healthcheck.test[0]" <<<"$COMPOSE_JSON")" = "CMD" ] || fail "$1 healthcheck must use the exec form (CMD)"
  jq_ -r ".services.$1.healthcheck.test[1:][]" <<<"$COMPOSE_JSON"
}
mapfile -t API_CMD < <(cmd_of api)
mapfile -t WEB_CMD < <(cmd_of web)
mapfile -t KC_CMD < <(cmd_of keycloak)

ASPNET=mcr.microsoft.com/dotnet/aspnet:10.0
docker run -d --name ptb-h-api python:3-alpine \
  sh -c 'mkdir -p /srv/api && echo ok > /srv/api/health && cd /srv && python -u -m http.server 8080' >/dev/null
docker run -d --name ptb-h-api404 python:3-alpine sh -c 'mkdir -p /srv && cd /srv && python -u -m http.server 8080' >/dev/null
listening() { docker exec "$1" python -c "import socket; socket.create_connection(('127.0.0.1', 8080))"; }
wait_for 20 listening ptb-h-api || fail "stub api server did not start"
wait_for 20 listening ptb-h-api404 || fail "stub 404 server did not start"
docker run --rm --network container:ptb-h-api $ASPNET "${API_CMD[@]}" || fail "api healthcheck fails against a healthy endpoint"
if docker run --rm --network container:ptb-h-api404 $ASPNET "${API_CMD[@]}" >/dev/null 2>&1; then fail "api healthcheck passes when /api/health returns 404"; fi
if docker run --rm $ASPNET "${API_CMD[@]}" >/dev/null 2>&1; then fail "api healthcheck passes when nothing is listening"; fi

docker create --name ptb-h-web caddy:2-alpine >/dev/null
docker cp ../web/Caddyfile ptb-h-web:/etc/caddy/Caddyfile
docker start ptb-h-web >/dev/null
docker exec ptb-h-web sh -c 'echo ok > /usr/share/caddy/index.html'
wait_for 20 docker exec ptb-h-web "${WEB_CMD[@]}" || fail "web healthcheck fails while the site is served"
docker exec ptb-h-web rm /usr/share/caddy/index.html
if docker exec ptb-h-web "${WEB_CMD[@]}" >/dev/null 2>&1; then fail "web healthcheck passes when the site returns an error"; fi

KC_IMAGE=quay.io/keycloak/keycloak:26.0
if docker run --rm --entrypoint '' $KC_IMAGE "${KC_CMD[@]}" >/dev/null 2>&1; then fail "keycloak healthcheck passes when nothing is listening"; fi
# Production mode: `start` against Postgres, with every KC_* setting taken from the compose file
# (only the database address and credentials point at a throwaway Postgres instead).
docker run -d --name ptb-h-db --network $NET -e POSTGRES_PASSWORD=pw -e POSTGRES_DB=keycloak postgres:16-alpine >/dev/null
mapfile -t KC_SETTINGS < <(jq_ -r '.services.keycloak.environment | to_entries[] | select(.key | startswith("KC_")) | "\(.key)=\(.value)"' <<<"$COMPOSE_JSON")
KC_ARGS=()
for setting in "${KC_SETTINGS[@]}"; do KC_ARGS+=(-e "$setting"); done
printf '%s\n' "${KC_SETTINGS[@]}" | grep -qx 'KC_HEALTH_ENABLED=true' || fail "keycloak must set KC_HEALTH_ENABLED=true"
docker run -d --name ptb-h-kc --network $NET "${KC_ARGS[@]}" -e KC_DB_URL=jdbc:postgresql://ptb-h-db:5432/keycloak \
  -e KC_DB_USERNAME=postgres -e KC_DB_PASSWORD=pw $KC_IMAGE start >/dev/null
echo "waiting for keycloak (production mode: the first start also builds)..."
wait_for 300 docker exec ptb-h-kc "${KC_CMD[@]}" || { docker logs ptb-h-kc 2>&1 | tail -15; fail "keycloak healthcheck never passed"; }

echo "PASS"
