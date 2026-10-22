#!/bin/sh
# Dump the app and Keycloak databases, encrypt each with age, upload to S3-compatible storage (R2),
# prune old dumps, then ping the heartbeat. Any failure exits non-zero BEFORE the heartbeat, so the
# missing ping is what raises the alert.
# Env: PGHOST/PGUSER/PGPASSWORD, POSTGRES_DB (app database name), BACKUP_AGE_RECIPIENT (age public key),
#      BACKUP_S3_* (see s3-env.sh), BACKUP_HEARTBEAT_URL, optional BACKUP_RETENTION (rclone duration,
#      default 14d).
set -eu
. /usr/local/bin/s3-env.sh
: "${BACKUP_AGE_RECIPIENT:?BACKUP_AGE_RECIPIENT is not set}"
: "${BACKUP_HEARTBEAT_URL:?BACKUP_HEARTBEAT_URL is not set}"

databases="${POSTGRES_DB:-pubtracker_db} keycloak"
retention="${BACKUP_RETENTION:-14d}"
stamp=$(date -u +%Y%m%dT%H%M%SZ)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

for db in $databases; do
  echo "dumping $db"
  pg_dump -Fc -d "$db" -f "$work/$db.dump"
  age -r "$BACKUP_AGE_RECIPIENT" -o "$work/$db.dump.age" "$work/$db.dump"
  rm "$work/$db.dump"
  object="BK:$BACKUP_S3_BUCKET/$db/$db-$stamp.dump.age"
  rclone copyto "$work/$db.dump.age" "$object"
  rclone lsf "$object" | grep -q . # copyto verifies size/hash; this confirms the object is listed
  rm "$work/$db.dump.age"
done

# Only after every upload succeeded, so a broken run never shrinks the existing history.
for db in $databases; do
  rclone delete "BK:$BACKUP_S3_BUCKET/$db" --min-age "$retention"
done

echo "backup $stamp complete"
curl -fsS -m 10 --retry 3 -o /dev/null "$BACKUP_HEARTBEAT_URL" || echo "heartbeat ping failed" >&2
