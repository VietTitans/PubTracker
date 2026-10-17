# Database backups and restore drill

The `backup` service in `docker/docker-compose.prod.yml` dumps the app database and the Keycloak database every night at 03:15 UTC, encrypts each dump with `age`, and uploads it to an EU Cloudflare R2 bucket. Dumps older than 14 days are deleted after a successful run. A heartbeat is pinged only after everything succeeded, so a missing ping is the alert.

Deleted accounts age out of backups with the 14-day retention (see the privacy policy for the combined retention figure).

## One-time setup

1. **Bucket.** In Cloudflare R2 create a bucket with the **EU jurisdiction**. Create an API token with *Object Read & Write* scoped to that bucket only.
2. **Encryption key.** On your own machine (not the VM): `age-keygen -o pubtracker-backup.key`. Store the file in your password manager and keep an offline copy. The line `Public key: age1...` is the recipient.
3. **Heartbeat.** In Healthchecks.io create a check with a 1-day period and ~2-hour grace; note its ping URL.
4. **VM `.env`** (see `docker/.env.prod.example`): set `BACKUP_AGE_RECIPIENT` (the public key), `BACKUP_S3_ENDPOINT` (`https://<account-id>.eu.r2.cloudflarestorage.com`), `BACKUP_S3_ACCESS_KEY_ID`, `BACKUP_S3_SECRET_ACCESS_KEY`, `BACKUP_S3_BUCKET`, `BACKUP_HEARTBEAT_URL`.
5. Deploy. The container refuses to start if any required value is missing, so a misconfiguration shows up as a restarting `backup` container, not as a silent night with no backup.

**Never put the private key on the VM.** Anyone with the VM could then decrypt every backup.

## Run a backup now

```sh
cd ~/pubtracker
docker compose -f docker-compose.prod.yml run --rm backup backup.sh
```

The planned safe-deploy work (issue #11) will reuse this command for a pre-deploy dump.

## Restore drill

Run the drill **on your own workstation** (needs Docker), never on the VM: the private key must not leave your machine. `restore.sh` restores into a **new** database and refuses if the target name already exists, so it can never overwrite live data. If the restore itself fails it drops the half-built target database.

You need a read-only R2 token for the bucket (create a separate one for drills so the VM's write token stays off your workstation).

1. Build the image and start a scratch Postgres:
   ```sh
   docker build -t pt-backup docker/backup
   docker network create pt-drill
   docker run -d --name pt-restore --network pt-drill -e POSTGRES_PASSWORD=scratch postgres:16-alpine
   ```
2. Restore each database (the key file is mounted from your machine):
   ```sh
   docker run --rm --network pt-drill \
     -e PGHOST=pt-restore -e PGUSER=postgres -e PGPASSWORD=scratch \
     -e BACKUP_S3_ENDPOINT=<r2 endpoint> -e BACKUP_S3_ACCESS_KEY_ID=<read-only id> \
     -e BACKUP_S3_SECRET_ACCESS_KEY=<read-only secret> -e BACKUP_S3_BUCKET=<bucket> \
     -e BACKUP_AGE_IDENTITY_FILE=/identity.txt -v "$PWD/pubtracker-backup.key:/identity.txt:ro" \
     pt-backup restore.sh pubtracker_db drill_app
   ```
   Repeat with `keycloak drill_kc`. Add a third argument to restore a specific object instead of the newest.
3. Check the data, e.g. `docker exec pt-restore psql -U postgres -d drill_app -c 'select count(*) from users'`, and compare with the live count.
4. Clean up: `docker rm -f pt-restore && docker network rm pt-drill`.

For a real disaster, restore the same way on a trusted machine into a new database, then load it into production (stop the API, swap the connection string or rename the databases).

## Tests

`docker/backup/test.sh` runs the whole backup, prune, encrypt, restore and failure path against a throwaway Postgres and an S3 stand-in. It needs a running Docker daemon and does not touch R2.

## Drill log

Record each drill against the real R2 bucket here.

- [ ] First restore drill from R2 (date, who, backup timestamp, result)
