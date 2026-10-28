#!/bin/sh
# NFR-REL-003 — sao lưu PostgreSQL hằng ngày, giữ BACKUP_RETENTION_DAYS ngày (mặc định 30).
# Chạy trong service pg-backup của docker-compose.prod.yml (crond 03:00 UTC). Chạy tay:
#   docker compose --env-file .env.production -f docker-compose.prod.yml exec pg-backup sh /usr/local/bin/pg-backup.sh
set -eu

# crond không truyền biến môi trường — entrypoint của service đã ghi chúng ra file này.
[ -f /etc/pg-backup.env ] && . /etc/pg-backup.env

: "${PGHOST:?}" "${PGDATABASE:?}" "${PGUSER:?}" "${PGPASSWORD:?}"
retention="${BACKUP_RETENTION_DAYS:-30}"

file="/backups/${PGDATABASE}-$(date -u +%Y%m%dT%H%M%SZ).dump"

# Ghi ra .tmp rồi đổi tên: lỗi giữa chừng không để lại file dump hỏng trông như bản tốt.
pg_dump -Fc -f "$file.tmp"
mv "$file.tmp" "$file"
echo "pg-backup: đã ghi $file ($(du -h "$file" | cut -f1))"

# -mtime +N nghĩa là cũ hơn N+1 ngày tròn → +(retention-1) giữ đúng $retention ngày.
find /backups -name "${PGDATABASE}-*.dump" -mtime "+$((retention - 1))" -print -delete
