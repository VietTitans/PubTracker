#!/usr/bin/env bash
# Tests deploy.sh with stand-ins for ssh, curl and sleep: no VM, no network, no Docker needed.
# The fake VM remembers which tag `up -d` last started; the fake health endpoint is down for BAD_TAG.
# Usage: deploy/test.sh
set -euo pipefail
cd "$(dirname "$0")"
DEPLOY="$PWD/deploy.sh"
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
mkdir "$WORK/bin"

cat > "$WORK/bin/ssh" <<'SH'
#!/bin/sh
for last; do :; done
cat > /dev/null
echo "$last" >> "$LOG"
# Like real compose, refuse to run when TAG is not set on `docker compose` itself.
case "$last" in
  *"docker compose"*) echo "$last" | grep -q 'TAG=[^ ]* docker compose' || { echo "TAG not set" >&2; exit 1; } ;;
esac
case "$last" in
  *"docker inspect"*) [ -z "${PREV_IMAGE:-}" ] || echo "$PREV_IMAGE" | sed 's/.*://' ;;
  *"backup.sh"*) [ "${DUMP_FAILS:-0}" != 1 ] || exit 1 ;;
  *"up -d"*) echo "$last" | sed -n 's/.*TAG=\([^ ]*\) docker compose.* up -d.*/\1/p' | tail -n 1 > "$STATE" ;;
esac
SH
cat > "$WORK/bin/curl" <<'SH'
#!/bin/sh
case " ${BAD_TAGS:-} " in *" $(cat "$STATE" 2>/dev/null) "*) exit 22 ;; esac
echo ok
SH
printf '#!/bin/sh\nexit 0\n' > "$WORK/bin/sleep"
chmod +x "$WORK/bin/"*

fail() { echo "FAIL: $*" >&2; exit 1; }

# run_deploy <new tag>: sets RC, and LOG/STATE files for assertions.
run_deploy() {
  export LOG="$WORK/log" STATE="$WORK/state"
  : > "$LOG"; rm -f "$STATE"
  RC=0
  PATH="$WORK/bin:$PATH" SSH_KEY=k DEPLOY_USER=u PUBLIC_ORIGIN=https://x HEALTH_TRIES=2 \
    sh "$DEPLOY" "$1" < /dev/null > "$WORK/out" 2>&1 || RC=$?
}
running() { cat "$STATE" 2>/dev/null || true; }

echo "== healthy deploy: dump first, then the new tag runs"
export PREV_IMAGE=ghcr.io/o/pubtracker-api:41 BAD_TAGS="" DUMP_FAILS=0
run_deploy 42
[ "$RC" = 0 ] || fail "healthy deploy should succeed (rc=$RC)"
[ "$(running)" = 42 ] || fail "expected 42 running, got '$(running)'"
grep -q 'backup.sh' "$LOG" || fail "no pre-deploy dump"
[ "$(grep -n 'backup.sh' "$LOG" | head -1 | cut -d: -f1)" -lt "$(grep -n 'pull' "$LOG" | head -1 | cut -d: -f1)" ] || fail "dump must come before pull"
grep -q 'previously running tag: 41' "$WORK/out" || fail "previous tag not reported"
grep -q 'echo 41 > ~/pubtracker/.previous_tag' "$LOG" || fail "previous tag not recorded on the VM"

echo "== failing health check: old tag runs again and the build fails"
BAD_TAGS="42"
run_deploy 42
[ "$RC" != 0 ] || fail "unhealthy deploy must fail the build"
[ "$(running)" = 41 ] || fail "expected rollback to 41, got '$(running)'"
grep -q 'rolled back to 41' "$WORK/out" || fail "rollback not reported"
grep -q 'TAG=41 docker compose -f docker-compose.prod.yml pull' "$LOG" || fail "rollback must pull the old images"

echo "== rollback target also unhealthy: still fails"
BAD_TAGS="42 41"
run_deploy 42
[ "$RC" != 0 ] || fail "must fail when rollback is unhealthy too"
grep -q 'also unhealthy' "$WORK/out" || fail "double failure not reported"

echo "== failed dump: nothing is pulled or started"
BAD_TAGS="" DUMP_FAILS=1
run_deploy 42
[ "$RC" != 0 ] || fail "failed dump must abort the deploy"
! grep -q 'pull' "$LOG" || fail "deploy went ahead after a failed dump"
[ -z "$(running)" ] || fail "something was started after a failed dump"

echo "== first deploy: no dump, no rollback target"
DUMP_FAILS=0 PREV_IMAGE=""
run_deploy 1
[ "$RC" = 0 ] || fail "healthy first deploy should succeed"
! grep -q 'backup.sh' "$LOG" || fail "first deploy should not dump"
BAD_TAGS="1"
run_deploy 1
[ "$RC" != 0 ] || fail "unhealthy first deploy must fail"
grep -q 'no previous tag' "$WORK/out" || fail "missing rollback target not reported"

echo "OK"
