#!/bin/sh
# Ship docker/ prod files to the VM, dump the databases, start the given image tag and wait for
# /api/health. If the new tag is not healthy, redeploy the previously running tag and still fail.
# Usage: deploy.sh <tag>
# Env: SSH_KEY (path), DEPLOY_USER, PUBLIC_ORIGIN, DEPLOY_HOST (default ssh.pubtracker.org),
#      TUNNEL_SERVICE_TOKEN_ID / TUNNEL_SERVICE_TOKEN_SECRET (Cloudflare Access service token,
#      read by cloudflared itself). The VM needs ~/pubtracker/.env and a one-time `docker login ghcr.io`
#      (or public GHCR packages). HEALTH_TRIES (default 30, 5 s apart) is for tests.
#      See docs/adr/0001 and docs/runbooks/rollback.md.
set -eu
TAG=${1:?usage: deploy.sh <tag>}
HOST=${DEPLOY_HOST:-ssh.pubtracker.org}
HEALTH_TRIES=${HEALTH_TRIES:-30}
SSH="ssh -i $SSH_KEY -o StrictHostKeyChecking=accept-new -o ProxyCommand=\"cloudflared access ssh --hostname %h\" $DEPLOY_USER@$HOST"

# Runs one command string on the VM. The string must reach ssh as a single argument, or `&&` and `~`
# would be handled by the local shell.
remote() { eval "$SSH \"\$1\""; }

# compose <tag> <args>: the VM command for `docker compose` run as that tag. TAG must be set on
# `docker compose` itself, not on the `cd` before it: every service in the file interpolates it.
compose() { echo "cd ~/pubtracker && TAG=$1 docker compose -f docker-compose.prod.yml $2"; }

healthy() {
  i=0
  while [ "$i" -lt "$HEALTH_TRIES" ]; do
    curl -fsS --connect-timeout 5 --max-time 10 "$PUBLIC_ORIGIN/api/health" && echo && return 0
    i=$((i + 1))
    sleep 5
  done
  return 1
}

cd "$(dirname "$0")/../docker"
tar cf - docker-compose.prod.yml caddy/Caddyfile.prod keycloak/realm-export.prod.json postgres-init backup host \
  | remote 'mkdir -p ~/pubtracker && tar xf - -C ~/pubtracker'

# Record what is running now, before anything is pulled: it is the rollback target.
# `docker ps -a` failing (daemon down) aborts the script; only a clean "no api container" is a first deploy.
FIND_PREV='names=$(docker ps -a --format "{{.Names}}") && if echo "$names" | grep -qx api; then docker inspect --format "{{.Config.Image}}" api | sed "s/.*://"; fi'
PREV=$(remote "$FIND_PREV")
if [ -n "$PREV" ]; then
  echo "previously running tag: $PREV"
  # A failed dump aborts the deploy before anything changes. Nothing to dump on a first deploy.
  remote "$(compose "$PREV" 'run --rm backup backup.sh')" || { echo "pre-deploy database dump failed; not deploying" >&2; exit 1; }
  remote "echo $PREV > ~/pubtracker/.previous_tag"
else
  echo "no running api container found: first deploy, so no dump and no rollback target"
fi

remote "$(compose "$TAG" pull) && $(compose "$TAG" 'up -d --build --remove-orphans')"
healthy && exit 0

echo "health check failed for $TAG" >&2
if [ -z "$PREV" ]; then
  echo "no previous tag to roll back to" >&2
  exit 1
fi
echo "rolling back to $PREV" >&2
remote "$(compose "$PREV" 'up -d --remove-orphans')"
if healthy; then
  echo "rolled back to $PREV; failing the build because $TAG was unhealthy" >&2
else
  echo "rollback to $PREV is also unhealthy: see docs/runbooks/rollback.md" >&2
fi
exit 1
