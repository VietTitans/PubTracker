# Deploys, automatic rollback and manual rollback

`deploy/deploy.sh <tag>` (run by the Jenkins `Deploy` stage on `main`) does this on the VM:

1. Records the tag the `api` container is running now (also written to `~/pubtracker/.previous_tag`).
2. Takes a database dump with `docker compose run --rm backup backup.sh` (the nightly backup tooling: encrypted, uploaded to R2). If the dump fails, the deploy stops before anything changes. A first deploy has nothing to dump.
3. Pulls and starts the new tag.
4. Polls `$PUBLIC_ORIGIN/api/health` for up to 150 seconds. If it never answers, it pulls and starts the previous tag again, waits for health, and **still exits non-zero**, so the build is red whichever way the rollback went.

`deploy/test.sh` checks these steps with a fake VM; it needs no Docker.

## What a rollback cannot undo

Rolling back changes the image, not the database. Migrations run when the new `api` starts and are not reversed. This is why every migration must stay compatible with the previous image (see CLAUDE.md): the old image has to keep working against the new schema. `MigrationSafetyTests` fails the build for the common ways of breaking it. If a migration broke that rule anyway, an automatic rollback leaves a running-but-broken old image. Then restore the pre-deploy dump (the "Restore drill" steps in `backup-restore.md`, loaded into production by hand).

## Rolling back by hand, from a laptop

Needs `cloudflared`, the deploy SSH key, and a Cloudflare Access login or service token for `ssh.pubtracker.org`.

1. Connect through the Access hostname (same ProxyCommand as `deploy.sh`):
   ```sh
   ssh -i <key> -o ProxyCommand="cloudflared access ssh --hostname %h" <user>@ssh.pubtracker.org
   ```
2. See what is running and what the last deploy replaced:
   ```sh
   cd ~/pubtracker
   docker ps --format '{{.Names}} {{.Image}} {{.Status}}'
   cat .previous_tag
   ```
3. Start the older tag (a Jenkins build number; the images stay in GHCR):
   ```sh
   TAG=<old tag> docker compose -f docker-compose.prod.yml pull
   TAG=<old tag> docker compose -f docker-compose.prod.yml up -d --remove-orphans
   ```
4. Check `docker ps` shows `api`, `web`, `keycloak`, `db` as `(healthy)` and `curl -fsS https://pubtracker.org/api/health` answers.
5. Fix forward in git. Do not leave production on the old tag without a ticket: the next successful `main` build deploys the newer code again.

## Drill log

Follow the manual steps once against the real VM (a harmless redeploy of the current tag works: `TAG=<current>` in step 3) and record it here.

- [ ] First manual rollback drill (date, who, from tag, to tag, result)
