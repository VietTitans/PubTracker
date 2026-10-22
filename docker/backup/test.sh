#!/usr/bin/env bash
# Round-trip test for backup.sh / restore.sh against throwaway Postgres + S3Mock (stand-in for R2).
# Needs a running Docker daemon. Usage: docker/backup/test.sh
set -euo pipefail
export MSYS_NO_PATHCONV=1 # Git Bash on Windows would otherwise rewrite /keys/... style args
cd "$(dirname "$0")"

NET=ptb-test
IMG=ptb-backup-test
fail() { echo "FAIL: $*" >&2; exit 1; }
cleanup() {
  docker rm -f ptb-db ptb-s3 ptb-hb >/dev/null 2>&1 || true
  docker volume rm -f ptb-keys >/dev/null 2>&1 || true
  docker network rm $NET >/dev/null 2>&1 || true
}
trap cleanup EXIT
cleanup

docker build -q -t $IMG . >/dev/null
docker network create $NET >/dev/null
docker volume create ptb-keys >/dev/null
docker run -d --name ptb-db --network $NET -e POSTGRES_PASSWORD=pw postgres:16-alpine >/dev/null
docker run -d --name ptb-s3 --network $NET -e COM_ADOBE_TESTING_S3MOCK_STORE_INITIAL_BUCKETS=ptb-backups \
  adobe/s3mock >/dev/null
docker run -d --name ptb-hb --network $NET python:3-alpine \
  sh -c 'cd /tmp && echo ok > ping && python -u -m http.server 8080' >/dev/null

S3_ENV=(-e BACKUP_S3_ENDPOINT=http://ptb-s3:9090 -e BACKUP_S3_ACCESS_KEY_ID=test
        -e BACKUP_S3_SECRET_ACCESS_KEY=test -e BACKUP_S3_BUCKET=ptb-backups
        -e BACKUP_S3_PROVIDER=Other -e BACKUP_S3_REGION=us-east-1)
PG_ENV=(-e PGHOST=ptb-db -e PGUSER=postgres -e PGPASSWORD=pw)

psql_() { docker run --rm --network $NET "${PG_ENV[@]}" $IMG psql -v ON_ERROR_STOP=1 -tA "$@"; }
rc_() { docker run --rm --network $NET "${S3_ENV[@]}" $IMG sh -c ". /usr/local/bin/s3-env.sh; $1"; }
hits() { docker logs ptb-hb 2>&1 | grep -c 'GET /ping' || true; }

# Retry a command for up to ~2 minutes; a broken setup fails the test instead of hanging it.
wait_for() { for _ in $(seq 120); do "$@" >/dev/null 2>&1 && return 0; sleep 1; done; fail "timed out waiting for: $*"; }

echo "waiting for postgres and s3mock..."
wait_for psql_ -c 'select 1'
# The bucket is pre-created by S3Mock (prod uses no_check_bucket, so rclone never creates it).
wait_for rc_ 'rclone lsf "BK:$BACKUP_S3_BUCKET"'

psql_ -c 'create database pubtracker_db' -c 'create database keycloak' >/dev/null
psql_ -d pubtracker_db -c "create table users(id int, email text); insert into users values (1,'ada-marker-7f3a@example.org'),(2,'b@x.org'),(3,'c@y.org')" >/dev/null
psql_ -d keycloak -c "create table realm(id int, name text); insert into realm values (1,'science-alerts-saas')" >/dev/null

RECIPIENT=$(docker run --rm -v ptb-keys:/keys $IMG sh -c 'age-keygen -o /keys/identity.txt 2>&1 | sed "s/^Public key: //"')
BACKUP_ENV=(--network $NET "${S3_ENV[@]}" "${PG_ENV[@]}" -e POSTGRES_DB=pubtracker_db
            -e BACKUP_AGE_RECIPIENT="$RECIPIENT" -e BACKUP_HEARTBEAT_URL=http://ptb-hb:8080/ping)

echo "== backup uploads an encrypted dump of both databases and pings the heartbeat =="
docker run --rm "${BACKUP_ENV[@]}" $IMG backup.sh >/dev/null
[ "$(hits)" = 1 ] || fail "expected 1 heartbeat after a successful backup, got $(hits)"
for db in pubtracker_db keycloak; do
  [ "$(rc_ "rclone lsf BK:\$BACKUP_S3_BUCKET/$db" | grep -c '\.dump\.age$')" = 1 ] || fail "expected one $db dump in the bucket"
  header=$(rc_ "rclone cat BK:\$BACKUP_S3_BUCKET/$db/\$(rclone lsf BK:\$BACKUP_S3_BUCKET/$db | head -n 1) | head -c 21")
  [ "$header" = "age-encryption.org/v1" ] || fail "$db dump is not age-encrypted (header: $header)"
done

echo "== the next backup prunes dumps older than the retention window =="
first=$(rc_ 'rclone lsf BK:$BACKUP_S3_BUCKET/pubtracker_db')
sleep 3
docker run --rm "${BACKUP_ENV[@]}" -e BACKUP_RETENTION=2s $IMG backup.sh >/dev/null
second=$(rc_ 'rclone lsf BK:$BACKUP_S3_BUCKET/pubtracker_db')
[ "$(echo "$second" | wc -l)" = 1 ] || fail "expected only the new dump to remain, got: $second"
[ "$first" != "$second" ] || fail "the old dump was not pruned"
[ "$(hits)" = 2 ] || fail "expected 2 heartbeats, got $(hits)"

echo "== restore.sh rebuilds both databases from the encrypted dumps =="
RESTORE_ENV=(--network $NET "${S3_ENV[@]}" "${PG_ENV[@]}" -e BACKUP_AGE_IDENTITY_FILE=/keys/identity.txt -v ptb-keys:/keys:ro)
docker run --rm "${RESTORE_ENV[@]}" $IMG restore.sh pubtracker_db restored_app >/dev/null
docker run --rm "${RESTORE_ENV[@]}" $IMG restore.sh keycloak restored_kc >/dev/null
[ "$(psql_ -d restored_app -c 'select count(*) from users')" = 3 ] || fail "restored users table has the wrong row count"
[ "$(psql_ -d restored_app -c 'select email from users where id = 1')" = "ada-marker-7f3a@example.org" ] || fail "restored row content differs"
[ "$(psql_ -d restored_kc -c 'select name from realm')" = "science-alerts-saas" ] || fail "restored keycloak data differs"
if docker run --rm "${RESTORE_ENV[@]}" $IMG restore.sh pubtracker_db restored_app >/dev/null 2>&1; then
  fail "restore.sh must refuse to restore into a database that already exists"
fi

echo "== an unreachable store fails the run and sends no heartbeat =="
before=$(hits)
if docker run --rm "${BACKUP_ENV[@]}" -e BACKUP_S3_ENDPOINT=http://nowhere.invalid:9000 \
     -e RCLONE_RETRIES=1 -e RCLONE_LOW_LEVEL_RETRIES=1 $IMG backup.sh >/dev/null 2>&1; then
  fail "backup.sh must exit non-zero when the upload fails"
fi
[ "$(hits)" = "$before" ] || fail "a failed backup must not ping the heartbeat"

echo "PASS"
