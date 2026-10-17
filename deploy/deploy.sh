#!/bin/sh
# Ship docker/ prod files to the VM, start the given image tag, wait for /api/health.
# Usage: deploy.sh <tag>
# Env: SSH_KEY (path), DEPLOY_USER, PUBLIC_ORIGIN, DEPLOY_HOST (default ssh.pubtracker.org),
#      TUNNEL_SERVICE_TOKEN_ID / TUNNEL_SERVICE_TOKEN_SECRET (Cloudflare Access service token,
#      read by cloudflared itself). The VM needs ~/pubtracker/.env and a one-time `docker login ghcr.io`
#      (or public GHCR packages). See docs/adr/0001.
set -eu
TAG=${1:?usage: deploy.sh <tag>}
HOST=${DEPLOY_HOST:-ssh.pubtracker.org}
SSH="ssh -i $SSH_KEY -o StrictHostKeyChecking=accept-new -o ProxyCommand=\"cloudflared access ssh --hostname %h\" $DEPLOY_USER@$HOST"

cd "$(dirname "$0")/../docker"
tar cf - docker-compose.prod.yml caddy/Caddyfile keycloak/realm-export.prod.json postgres-init backup \
  | eval "$SSH" 'mkdir -p ~/pubtracker && tar xf - -C ~/pubtracker'
eval "$SSH" "cd ~/pubtracker && TAG=$TAG docker compose -f docker-compose.prod.yml pull && TAG=$TAG docker compose -f docker-compose.prod.yml up -d --build --remove-orphans"

for i in $(seq 1 30); do
  curl -fsS "$PUBLIC_ORIGIN/api/health" && echo && exit 0
  sleep 5
done
echo "health check failed for $TAG" >&2
exit 1
