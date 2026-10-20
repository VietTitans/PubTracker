#!/bin/sh
# Host cron job (runs on the VM, not in a container): pings a Healthchecks.io check while disk usage
# is under the threshold, and its /fail endpoint at or over it. A cron job that stops running is
# caught too, because the missing ping makes the check late.
# Env: DISK_ALERT_PING_URL (required), DISK_ALERT_PATH (default /), DISK_ALERT_THRESHOLD (percent, default 80).
set -eu
: "${DISK_ALERT_PING_URL:?DISK_ALERT_PING_URL is not set}"
path=${DISK_ALERT_PATH:-/}
threshold=${DISK_ALERT_THRESHOLD:-80}

used=$(df -P "$path" | awk 'NR == 2 { gsub("%", "", $5); print $5 }')
if [ "$used" -ge "$threshold" ]; then
  echo "disk $path is ${used}% full (threshold ${threshold}%)" >&2
  url="$DISK_ALERT_PING_URL/fail"
else
  url="$DISK_ALERT_PING_URL"
fi
curl -fsS -m 10 --retry 3 -o /dev/null "$url"
