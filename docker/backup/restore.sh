#!/bin/sh
# Restore the newest encrypted dump of <source-db> into a NEW database <target-db> on the server in
# PGHOST/PGUSER/PGPASSWORD. Refuses if <target-db> exists, so it can never overwrite live data.
# Usage: restore.sh <source-db> <target-db> [object-name]
# Env: BACKUP_S3_* (see s3-env.sh), BACKUP_AGE_IDENTITY_FILE (age private key; keep it off the VM).
set -eu
. /usr/local/bin/s3-env.sh
source_db=${1:?usage: restore.sh <source-db> <target-db> [object-name]}
target_db=${2:?usage: restore.sh <source-db> <target-db> [object-name]}
: "${BACKUP_AGE_IDENTITY_FILE:?BACKUP_AGE_IDENTITY_FILE is not set}"

# Object names embed a UTC timestamp, so the lexically last one is the newest.
object=${3:-$(rclone lsf "BK:$BACKUP_S3_BUCKET/$source_db" | sort | tail -n 1)}
[ -n "$object" ] || { echo "no dumps found for $source_db" >&2; exit 1; }

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

echo "restoring $source_db/$object into $target_db"
rclone copyto "BK:$BACKUP_S3_BUCKET/$source_db/$object" "$work/dump.age"
age -d -i "$BACKUP_AGE_IDENTITY_FILE" -o "$work/dump" "$work/dump.age"
createdb "$target_db"
# We just created target_db, so dropping it on failure never touches pre-existing data.
pg_restore --exit-on-error --no-owner --no-acl -d "$target_db" "$work/dump" || { dropdb "$target_db"; exit 1; }
echo "restore complete"
