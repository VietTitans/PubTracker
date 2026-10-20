# VM hygiene: healthchecks, logs, patching, disk alert, reboot test

What the production stack does by itself, what you set up once on the VM, and how to verify each piece. The automated part (compose wiring, the healthcheck commands inside the real images, the disk alert script) is checked by `bash docker/test-hygiene.sh`, which needs a running Docker daemon and no VM. It starts a real Keycloak in production mode against a throwaway Postgres, so allow a few minutes.

## What the compose file already does

- **Healthchecks.** `api`, `web`, `keycloak` and `db` report health. `docker ps` shows `(healthy)`. Dependents wait for `service_healthy`, so `up -d` brings services up in order: db, then keycloak, then api, then web and the proxy. The api check calls `/api/health` (which also checks the database); Keycloak's follows its `/auth` prefix, `/auth/health/ready`.
  **Deploy-time effect:** `up -d` waits for those checks, and the proxy starts only after web, api and Keycloak are healthy. If a service never turns healthy, `up -d` fails, the deploy fails, and a first-ever deploy leaves the site down. Look at `docker compose -f docker-compose.prod.yml ps` and `docker logs <service>` to see which one. A service that is already running and unchanged is not restarted by a deploy.
- **Log rotation.** Every service keeps at most 3 files of 10 MB. Verify: `docker inspect --format '{{.HostConfig.LogConfig}}' api` shows `max-size:10m max-file:3`.
- **PEDro off.** The image is built without Chromium, so `Sources__Pedro__Enabled` is pinned to `false` in the compose file and cannot be changed from `.env`. Verify on the VM: `docker exec api printenv Sources__Pedro__Enabled` prints `false`. The code also defaults PEDro off (`SourceCatalogTests`).

## One-time VM setup

### Security updates (Ubuntu)

Check the OS first: `cat /etc/os-release`. These steps are for Ubuntu; on Oracle Linux use `dnf-automatic` instead.

Per the [Ubuntu server docs](https://ubuntu.com/server/docs/how-to/software/automatic-updates/), the `unattended-upgrades` package is installed by default and security updates are enabled right after installation. If it is missing: `sudo apt install unattended-upgrades`.

- Config that turns it on: `/etc/apt/apt.conf.d/20auto-upgrades` should contain `APT::Periodic::Update-Package-Lists "1";` and `APT::Periodic::Unattended-Upgrade "1";`
- Check it works: `sudo unattended-upgrade -v --dry-run`
- Logs: `/var/log/unattended-upgrades/`
- **Reboots stay manual** (`Unattended-Upgrade::Automatic-Reboot` defaults to `false`; leave it). Kernel updates need a reboot, so once a month check `ls /var/run/reboot-required`. If it exists, reboot in a quiet hour and watch the stack come back (see the reboot test below).

### Disk alert at 80%

1. In Healthchecks.io create a check named `vm-disk`, period 30 minutes, grace 15 minutes. Copy its ping URL.
2. The deploy ships `host/disk-alert.sh` to `~/pubtracker/host/`. The VM needs `curl`.
3. Add a cron entry (`crontab -e`), every 15 minutes:
   ```
   */15 * * * * DISK_ALERT_PING_URL=https://hc-ping.com/<uuid> sh $HOME/pubtracker/host/disk-alert.sh
   ```
4. How it alerts: under 80% it pings the check; at or over 80% it pings `<url>/fail` (Healthchecks' documented [failure signal](https://healthchecks.io/docs/signaling_failures/)). If cron or the VM dies, the missing ping makes the check late and it alerts anyway. Change the threshold with `DISK_ALERT_THRESHOLD=<percent>`.
5. Test it once: `DISK_ALERT_THRESHOLD=1 DISK_ALERT_PING_URL=<url> sh ~/pubtracker/host/disk-alert.sh` must turn the check red.

Alert delivery (email and phone push) is configured on the Healthchecks integrations page. Until you add a channel, nobody is notified. Issue #13 (observability and alerts) covers the channels.

## Reboot test (run once before launch)

The goal: after a reboot, the stack and the tunnel come back with nobody touching anything.

1. Before: `docker ps` (note the services) and `sudo systemctl is-enabled docker cloudflared`. Both must print `enabled`. The repo does not record how `cloudflared` was installed on the VM (see `deploy/cloudflared-config.example.yml`). If it is not a systemd unit named `cloudflared`, check whatever starts it, and fix this step to match.
2. `sudo reboot`.
3. Wait about 5 minutes. Reconnect over `ssh.pubtracker.org` (this proves the tunnel returned) and run `docker ps`. All services must be `Up`, and `api`, `web`, `keycloak`, `db` must show `(healthy)`.
4. From outside: `curl -fsS https://pubtracker.org/api/health`.
5. Record the result below.

Containers use `restart: unless-stopped`, which restarts them when the Docker daemon starts. Compose `depends_on` ordering applies only to `docker compose up`, not to a reboot, so services may restart in any order and rely on their own retries until the healthchecks go green.

## Drill log

- [ ] Reboot test (date, who, time until all services healthy, result)
- [ ] Disk alert test turned the Healthchecks check red
- [ ] `unattended-upgrade --dry-run` ran clean on the VM
