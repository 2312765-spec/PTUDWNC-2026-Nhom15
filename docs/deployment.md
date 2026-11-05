# Triển khai Production — `docker-compose.prod.yml`

> CONS-009 · NFR-SEC-005 · NFR-SEC-007 · NFR-REL-003. Quyết định: D58 (URL ảnh), D59 (migration),
> D60 (cổng), D66 (S3 = RustFS). Kế hoạch: `docs/plans/CONS-009-docker-compose-prod.md`,
> `docs/plans/CONS-009-prod-rustfs.md`. Dev xem `docker-compose.yml`.

## Kiến trúc

```
Internet ──► nginx :80  ── 301 ──► :443
             nginx :443 (TLS 1.2/1.3, HSTS 1 năm)
               ├─ /api/, /health   → api:8080
               ├─ /sitemap.xml     → minio:9000/culinary-blog/seo/sitemap.xml   (D52)
               ├─ /culinary-blog/  → minio:9000 (chỉ GET/HEAD — ảnh public, D58)
               └─ /                → frontend:3000
mạng internal: postgres · redis · minio · minio-init · migrate · api · frontend · pg-backup
```

Service `minio` chạy **RustFS** (S3 tương thích MinIO — D66); giữ tên service để Nginx và cấu hình
`Minio:*` của API không đổi. `minio-init` (aws-cli) tạo bucket `culinary-blog` + policy chỉ cho GET ẩn danh.

Chỉ `nginx` publish cổng ra host (D60). Thứ tự khởi động: postgres/redis/minio healthy →
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
Server Next.js tin cert này qua `NODE_EXTRA_CA_CERTS` (D58).

### 3. Image S3

Mặc định `rustfs/rustfs:1.0.3` và `amazon/aws-cli:2.38.0` — pull công khai, không cần chuẩn bị gì
(MinIO đã gỡ mọi image công khai — D65/D66). Dùng registry riêng thì đặt `S3_IMAGE` trong `.env.production`.

**Chuyển ảnh từ bản cũ chạy MinIO** (chỉ khi đã có dữ liệu thật trong volume `miniodata` — kiểm
2026-11-05 nhóm chưa có server prod): dựng stack mới (volume `s3data`), rồi trên máy còn image MinIO
chạy MinIO cũ tạm thời với volume `miniodata` trong cùng mạng, và
`aws s3 sync s3://culinary-blog s3://culinary-blog` từ endpoint cũ sang `http://minio:9000`
(hai lần `aws` với `--endpoint-url` khác nhau, qua thư mục tạm). Kiểm tra số object hai bên rồi mới xóa
volume `miniodata`.

## Chạy

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build --wait
./scripts/verify-prod.sh          # smoke test 9 bước
```

Mở `https://culinary.localhost`. Migration + 2 role (`Author`, `Admin`) do service `migrate` tạo
(D59) — **không** có dữ liệu mẫu.

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

Ảnh nằm trong volume `s3data` (RustFS) — sao lưu bằng snapshot volume hoặc
`aws --endpoint-url http://minio:9000 s3 sync s3://culinary-blog <thư mục>` (chạy qua service `minio-init`).

## Ngoài phạm vi

Tự gia hạn chứng chỉ (certbot), OTel Collector/Tempo/Prometheus (chỉ có sẵn biến
`OTEL_EXPORTER_OTLP_ENDPOINT`), S3 nhiều node, nhiều instance API, CDN, CI deploy tự động.
