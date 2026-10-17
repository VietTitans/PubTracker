#!/bin/sh
# Container entrypoint: run backup.sh every day at 03:15 UTC. A failed run is logged and the loop
# carries on; the missing heartbeat is what raises the alert.
set -eu
# Fail at container start, not at 03:15, if the configuration is incomplete.
. /usr/local/bin/s3-env.sh
: "${BACKUP_AGE_RECIPIENT:?BACKUP_AGE_RECIPIENT is not set}"
: "${BACKUP_HEARTBEAT_URL:?BACKUP_HEARTBEAT_URL is not set}"
: "${PGPASSWORD:?PGPASSWORD is not set}"

while :; do
  now=$(date +%s)
  next=$((now - now % 86400 + 3 * 3600 + 15 * 60))
  [ "$next" -gt "$now" ] || next=$((next + 86400))
  echo "next backup in $((next - now))s"
  sleep $((next - now))
  backup.sh || echo "backup failed" >&2
done
