# Sourced by backup.sh, restore.sh and schedule.sh: defines the rclone remote "BK" from BACKUP_S3_* env.
# Required: BACKUP_S3_ENDPOINT, BACKUP_S3_ACCESS_KEY_ID, BACKUP_S3_SECRET_ACCESS_KEY, BACKUP_S3_BUCKET.
# Optional: BACKUP_S3_PROVIDER (default Cloudflare, i.e. R2), BACKUP_S3_REGION (default auto).
: "${BACKUP_S3_ENDPOINT:?BACKUP_S3_ENDPOINT is not set}"
: "${BACKUP_S3_ACCESS_KEY_ID:?BACKUP_S3_ACCESS_KEY_ID is not set}"
: "${BACKUP_S3_SECRET_ACCESS_KEY:?BACKUP_S3_SECRET_ACCESS_KEY is not set}"
: "${BACKUP_S3_BUCKET:?BACKUP_S3_BUCKET is not set}"

export RCLONE_CONFIG_BK_TYPE=s3
export RCLONE_CONFIG_BK_PROVIDER="${BACKUP_S3_PROVIDER:-Cloudflare}"
export RCLONE_CONFIG_BK_ENDPOINT="$BACKUP_S3_ENDPOINT"
export RCLONE_CONFIG_BK_ACCESS_KEY_ID="$BACKUP_S3_ACCESS_KEY_ID"
export RCLONE_CONFIG_BK_SECRET_ACCESS_KEY="$BACKUP_S3_SECRET_ACCESS_KEY"
export RCLONE_CONFIG_BK_REGION="${BACKUP_S3_REGION:-auto}"
# The R2 token is scoped to the bucket and cannot create buckets; never let rclone try.
export RCLONE_CONFIG_BK_NO_CHECK_BUCKET=true
