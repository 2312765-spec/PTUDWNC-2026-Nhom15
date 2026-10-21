# Triển khai Production — `docker-compose.prod.yml`

> CONS-009 · NFR-SEC-005 · NFR-SEC-007 · NFR-REL-003. Quyết định: D55 (URL ảnh), D56 (migration),
> D57 (cổng). Kế hoạch: `docs/plans/CONS-009-docker-compose-prod.md`. Dev xem `docker-compose.yml`.

## Kiến trúc

```
Internet ──► nginx :80  ── 301 ──► :443
             nginx :443 (TLS 1.2/1.3, HSTS 1 năm)
               ├─ /api/, /health   → api:8080
               ├─ /sitemap.xml     → minio:9000/culinary-blog/seo/sitemap.xml   (D52)
               ├─ /culinary-blog/  → minio:9000 (chỉ GET/HEAD — ảnh public, D55)
               └─ /                → frontend:3000
mạng internal: postgres · redis · minio · minio-init · migrate · api · frontend · pg-backup
```

Chỉ `nginx` publish cổng ra host (D57). Thứ tự khởi động: postgres/redis/minio healthy →
`minio-init` + `migrate` chạy xong → `api` healthy (`/health/ready`) → `frontend` healthy → `nginx`.

## Chuẩn bị (một lần)

### 1. Biến môi trường

```bash
cp .env.production.example .env.production
openssl rand -base64 48        # chạy 4 lần: POSTGRES_PASSWORD, JWT_KEY, MINIO_ROOT_PASSWORD, AUTH_SECRET
```

`.env.production` nằm trong `.gitignore` — **không commit** (NFR-SEC-007).

> ⚠️ Khóa JWT cũ từng nằm trong `appsettings.json` và vẫn còn trong lịch sử Git — coi như đã lộ.
> **Luôn sinh `JWT_KEY` mới.** API không khởi động nếu thiếu khóa hoặc khóa ngắn hơn 32 byte.

### 2. Chứng chỉ TLS

Đặt vào `./ssl/` (đã `.gitignore`): `fullchain.pem` và `privkey.pem`.

Demo trên máy local (`SITE_DOMAIN=culinary.localhost` — trình duyệt và curl tự trỏ `*.localhost` về
127.0.0.1, không cần sửa file hosts):

```bash
mkdir -p ssl
MSYS_NO_PATHCONV=1 openssl req -x509 -newkey rsa:2048 -nodes -days 30 \
  -keyout ssl/privkey.pem -out ssl/fullchain.pem -subj "/CN=culinary.localhost" \
  -addext "subjectAltName=DNS:culinary.localhost" -addext "basicConstraints=critical,CA:TRUE"
```

(`MSYS_NO_PATHCONV=1` chỉ cần trên Git Bash Windows.) Trình duyệt sẽ cảnh báo cert tự ký — bình thường.
Server Next.js tin cert này qua `NODE_EXTRA_CA_CERTS` (D55).

### 3. Image MinIO

MinIO đã khóa pull ẩn danh trên cả quay.io và Docker Hub. Compose pin theo digest
(`RELEASE.2025-09-07T16-13-09Z`). Máy đã từng pull thì dùng được luôn; server mới:

```bash
# Trên máy đã có image
docker save quay.io/minio/minio quay.io/minio/mc -o minio-images.tar
# Trên server
docker load -i minio-images.tar
```

Hoặc đẩy lên registry riêng rồi đặt `MINIO_IMAGE` / `MC_IMAGE` trong `.env.production`.

## Chạy

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build --wait
./scripts/verify-prod.sh          # smoke test 8 bước
```

Mở `https://culinary.localhost`. Migration + 2 role (`Author`, `Admin`) do service `migrate` tạo
(D56) — **không** có dữ liệu mẫu.

Log: `docker compose --env-file .env.production -f docker-compose.prod.yml logs -f api`
(file JSON còn nằm trong volume `apilogs`).

## Sao lưu & khôi phục (NFR-REL-003)

Service `pg-backup` chạy `pg_dump -Fc` lúc **03:00 UTC** hằng ngày vào volume `pgbackups`, xóa bản cũ
hơn `BACKUP_RETENTION_DAYS` (mặc định 30).

```bash
P="docker compose --env-file .env.production -f docker-compose.prod.yml"

# Sao lưu ngay
$P exec pg-backup sh /usr/local/bin/pg-backup.sh
# Xem danh sách
$P exec pg-backup ls -lh /backups
# Khôi phục một bản (ghi đè dữ liệu hiện tại) — dừng api trước để không có ghi mới
$P stop api
$P exec pg-backup sh -c '. /etc/pg-backup.env && pg_restore --clean --if-exists -d "$PGDATABASE" /backups/<file>.dump'
$P start api
```

MinIO (ảnh) nằm trong volume `miniodata` — sao lưu bằng snapshot volume hoặc `mc mirror`.

## Ngoài phạm vi

Tự gia hạn chứng chỉ (certbot), OTel Collector/Tempo/Prometheus (chỉ có sẵn biến
`OTEL_EXPORTER_OTLP_ENDPOINT`), MinIO distributed, nhiều instance API, CDN, CI deploy tự động.
