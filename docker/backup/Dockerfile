# Nightly encrypted database backups (see docs/runbooks/backup-restore.md).
# pg_dump must match the server's major version (postgres:16 in docker-compose.prod.yml).
FROM alpine:3.21
RUN apk add --no-cache postgresql16-client age rclone curl
COPY s3-env.sh backup.sh restore.sh schedule.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/restore.sh /usr/local/bin/schedule.sh
CMD ["schedule.sh"]
