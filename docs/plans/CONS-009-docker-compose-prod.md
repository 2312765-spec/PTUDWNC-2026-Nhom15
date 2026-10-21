# Plan — CONS-009: `docker-compose.prod.yml` (triển khai Production)

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-10-14.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `feature/CONS-009-docker-compose-prod`. Việc tuần 8 theo `team-assignment.md`.
> 2026-10-21: đổi số quyết định D54–D56 → **D55–D57** (D54 đã được FR-AUTH-007 dùng).

---

## 1. Phạm vi

| Mã | Nội dung | Trạng thái |
|---|---|---|
| CONS-009 / SRS §6.5 | Prod dùng `docker-compose.prod.yml`, "optimized build + secrets management". Seq, Mailhog chỉ dev | ⬜ Chưa có file |
| NFR-SEC-005 | Mọi traffic qua HTTPS (TLS 1.2+), HTTP → 301 HTTPS, HSTS `max-age=31536000`, CORS không wildcard | ⬜ Nginx chỉ nghe :80 (TODO) |
| NFR-SEC-007 | Secret prod qua env_file, không commit | 🟡 `Jwt:Key` thật nằm trong `appsettings.json` |
| NFR-REL-003 | `pg_dump` tự động 03:00 hằng ngày, giữ 30 ngày · MinIO volume persistent | ⬜ |
| D29 (còn thiếu) | Prod chưa chạy migration + seed role → register/login 500 | ⬜ |
| D36 | Prod: trace + metric → OTel Collector qua `OTEL_EXPORTER_OTLP_ENDPOINT` | Chỉ cần truyền env |

**Ngoài phạm vi:** certbot / tự gia hạn chứng chỉ (dùng `./ssl/` mount sẵn), OTel Collector +
Tempo/Prometheus (chỉ để sẵn biến env), MinIO Distributed Mode và nhiều instance API (NFR-SCALE),
CDN, Kubernetes, CI deploy tự động.

## 2. Hiện trạng — vì sao chỉ viết file compose là chưa đủ

| # | Vấn đề | Hậu quả |
|---|---|---|
| 1 | `frontend/next.config.ts` thiếu `output: 'standalone'`, `frontend/Dockerfile` lại copy `.next/standalone` | Build image frontend lỗi |
| 2 | `NEXT_PUBLIC_*` được nhúng lúc **build**, Dockerfile không có `ARG` | Bundle trỏ sai URL |
| 3 | `MinioFileStorageService.PublicUrl` ghép từ `Minio:Endpoint` (nội bộ `http://minio:9000`) · `remotePatterns` chỉ có `localhost:9000` | Trình duyệt không tải được ảnh |
| 4 | Migrate + seed role chỉ trong `IsDevelopment()` | DB prod trống |
| 5 | `nginx/nginx.conf` chỉ :80 | Vi phạm NFR-SEC-005 |
| 6 | `Jwt:Key` thật đã commit trong `appsettings.json` | Vi phạm NFR-SEC-007 — coi như đã lộ |
| 7 | API không có `UseForwardedHeaders` | Sau Nginx: scheme luôn `http`, IP client là IP nginx (log, rate limit sai) |
| 8 | Bảng SRS §6.5 publish 5432/6379/9000 ra host | Lộ hạ tầng ra Internet |

## 3. Quyết định mới — thêm vào `docs/decisions.md` TRƯỚC khi code

| D | Chủ đề | Chốt |
|---|---|---|
| **D55** | URL công khai của ảnh MinIO | Thêm `Minio:PublicBaseUrl` (fallback `Minio:Endpoint` → dev giữ nguyên `http://localhost:9000`). Prod = `https://{domain}`; Nginx `location /culinary-blog/` proxy tới MinIO (chỉ GET/HEAD). Ảnh cùng origin với site → không cần domain MinIO riêng |
| **D56** | Migration + seed role ở prod | Service `migrate` một lần (cùng image API, `dotnet CulinaryBlog.API.dll --migrate-only`): `MigrateAsync` + `IdentityRoleSeeder` rồi thoát. **Không** chạy `DbSeeder` (dữ liệu mẫu). `api` `depends_on: migrate: service_completed_successfully` → không bao giờ có 2 instance cùng migrate |
| **D57** | Cổng prod | **Lệch bảng SRS §6.5:** chỉ `nginx` publish 80/443. postgres/redis/minio/api/frontend chỉ trong mạng `internal` của compose. Vì API chỉ nhận traffic từ Nginx, `ForwardedHeaders` tin mọi proxy trong mạng nội bộ (xóa `KnownNetworks/KnownProxies`) |

TLS: mount `./ssl/fullchain.pem` + `./ssl/privkey.pem` đúng SRS §6.5 — không cần D mới.

## 4. Kiến trúc prod

```
Internet ──► nginx :80  ── 301 ──► :443
             nginx :443 (TLS 1.2/1.3, HSTS)
               ├─ /api/, /health     → api:8080
               ├─ /sitemap.xml       → minio:9000/culinary-blog/seo/sitemap.xml   (D52)
               ├─ /culinary-blog/    → minio:9000 (GET/HEAD, ảnh public — D55)
               └─ /                  → frontend:3000
mạng internal: postgres · redis · minio · minio-init · migrate · api · frontend · pg-backup
```

Thứ tự khởi động: `postgres, redis, minio (healthy)` → `minio-init`, `migrate` (completed) →
`api (healthy /health/ready)` → `frontend` → `nginx`.

| Service | Image | Ghi chú |
|---|---|---|
| nginx | `nginx:1.27-alpine` | `nginx.prod.conf`, `./ssl:ro` |
| api | build `backend/CulinaryBlog.API/Dockerfile` | `env_file: .env.production`, `ASPNETCORE_ENVIRONMENT=Production` |
| migrate | cùng image api | `command: ["--migrate-only"]`, `restart: "no"` |
| frontend | build `./frontend`, build args `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_SITE_URL` | `AUTH_SECRET`, `AUTH_GOOGLE_*`, `API_INTERNAL_URL=http://api:8080/api/v1` |
| postgres | `postgres:16-alpine` | `pgdata`, `init-db.sql` (unaccent, pg_trgm) |
| redis | `redis:7-alpine` | `--appendonly yes`, `redisdata` |
| minio / minio-init | `quay.io/minio/minio` + `mc` (pin RELEASE tag) | `miniodata`, bucket public-read (D16), credentials từ env |
| pg-backup | `postgres:16-alpine` + crond | `pg_dump -Fc` 03:00, xóa file > 30 ngày, volume `pgbackups` |

Mọi service: `restart: unless-stopped`, healthcheck, giới hạn log `json-file` (max-size 10m × 3).
Không có `seq`, `mailhog`.

## 5. File theo tầng

### API
- `Program.cs` — nhánh `--migrate-only` (build app → migrate + seed role → return), `UseForwardedHeaders` đặt đầu pipeline, gọi kiểm tra `Jwt:Key` khi khởi động. *(File chung — báo A/B trước khi sửa.)*
- `Extensions/DatabaseMigrationExtensions.cs` *(mới)* — `MigrateAndSeedRolesAsync(IServiceProvider)`; nhánh Development dùng lại hàm này + `DbSeeder`.
- `Extensions/JwtKeyGuard.cs` *(mới)* — thiếu `Jwt:Key` hoặc < 32 ký tự → `InvalidOperationException` (fail fast).
- `appsettings.json` — `Jwt:Key` = `""`; thêm `Minio:PublicBaseUrl: ""`. Dev đặt khóa qua User Secrets / `appsettings.Development.json`.

### Infrastructure
- `Files/MinioFileStorageService.cs` — `PublicUrl` dùng `Minio:PublicBaseUrl ?? Minio:Endpoint`. `ExtractKey` vẫn tách theo `/{bucket}/` nên ảnh cũ vẫn xóa được.

### Frontend
- `next.config.ts` — `output: 'standalone'`; `remotePatterns` thêm host từ `NEXT_PUBLIC_SITE_URL` (`/culinary-blog/**`).
- `Dockerfile` — `ARG NEXT_PUBLIC_API_URL`, `ARG NEXT_PUBLIC_SITE_URL` → `ENV` ở stage build.

### Hạ tầng
- `docker-compose.prod.yml` *(mới)*
- `nginx/nginx.prod.conf` *(mới)* — giữ `nginx.conf` cho staging HTTP.
- `scripts/pg-backup.sh` *(mới)*
- `scripts/verify-prod.sh` *(mới)* — smoke test (mục 6).
- `.env.production.example` *(mới)* · `.gitignore` thêm `.env.production`, `ssl/`.

### Tài liệu
- `docs/decisions.md` D55–D57 · `README.md` mục "Triển khai production" (tạo `.env.production`,
  self-signed cert cho demo, `up -d`, khôi phục backup) · `traceability.md` (CONS-009,
  NFR-SEC-005, NFR-REL-003) · `CHANGELOG.md`.

## 6. Test — viết trước, xác nhận fail

**Tự động (xUnit):**

| Test | File | Fail hiện tại vì |
|---|---|---|
| `D55: có PublicBaseUrl → URL ảnh dùng PublicBaseUrl` | `IntegrationTests/Files/MinioFileStorageServiceTests.cs` | Chưa có cấu hình |
| `D55: không có PublicBaseUrl → fallback Endpoint (dev không đổi)` | như trên | (pass — hồi quy) |
| `D56: MigrateAndSeedRolesAsync trên DB trống → có bảng + 2 role` | `IntegrationTests/Common/DatabaseMigrationTests.cs` | Chưa có hàm |
| `D56: chạy 2 lần → idempotent` | như trên | Chưa có hàm |
| `NFR-SEC-007: Jwt:Key rỗng / < 32 ký tự → khởi động ném lỗi` | `IntegrationTests/Common/JwtKeyGuardTests.cs` (UnitTests chỉ tham chiếu Application, không thấy được tầng API) | Chưa có guard |
| `D57: X-Forwarded-Proto https → Request.Scheme = https` | `IntegrationTests/Common/ForwardedHeadersTests.cs` | Chưa `UseForwardedHeaders` |

**Smoke (`scripts/verify-prod.sh`, chạy trên stack prod với cert self-signed):**
1. `docker compose -f docker-compose.prod.yml config` hợp lệ.
2. Build được cả 2 image.
3. `http://localhost/` → 301 `https://`.
4. `https://localhost/health/ready` → 200; response có `Strict-Transport-Security: max-age=31536000`.
5. Cổng 5432/6379/9000/8080/3000 **không** mở trên host.
6. Register + login trên DB mới → 201/200 (migrate + seed role đã chạy).
7. Upload ảnh → URL trả về bắt đầu bằng `https://localhost/culinary-blog/` và GET được 200.
8. `/sitemap.xml` → 200 (sau khi trigger job) · trang chủ render 200.

## 7. Xong khi

- 6 test tự động pass, toàn bộ `dotnet test` pass, `dotnet build` 0 warning, `npm run lint && npm run build` pass.
- `verify-prod.sh` pass toàn bộ trên máy local.
- `decisions.md` có D55–D57; `traceability.md` + `CHANGELOG.md` + README cập nhật.
- Commit theo `feat(CONS-009): …`, PR có ≥ 1 reviewer (B theo vòng review).

## 8. Rủi ro / lưu ý

- **`Jwt:Key` cũ đã nằm trong lịch sử Git** — xóa khỏi `appsettings.json` không gỡ được lịch sử. Prod
  phải dùng khóa mới sinh ngẫu nhiên; báo cả nhóm, không viết lại lịch sử Git (cần cả nhóm đồng ý).
- `Program.cs` là file chung — báo nhóm trước, giữ thay đổi nhỏ.
- Đổi `Jwt:Key` thành rỗng không ảnh hưởng dev/test: `appsettings.Development.json` và `CulinaryBlogApiFactory` đã có khóa riêng.
- Image MinIO bị chặn pull trên Docker Hub (xem ghi chú FR-FILE trong traceability) → dùng `quay.io`, pin tag.

## 9. Kết quả (2026-10-21) — khác so với kế hoạch

- Đổi số quyết định thành **D55–D57** (D54 đã dùng cho FR-AUTH-007).
- `JwtKeyGuardTests` đặt ở IntegrationTests (UnitTests không tham chiếu tầng API).
- Hướng dẫn triển khai viết ở **`docs/deployment.md`** thay vì README — README hiện là bảng tiến độ nhóm.
- Phát sinh thêm, không có trong mục 2:
  - Server Next.js (SSR/ISR + `next/image`) gọi `https://{SITE_DOMAIN}` → nginx có network alias = domain,
    frontend tin cert qua `NODE_EXTRA_CA_CERTS`, bật `images.dangerouslyAllowLocalIP` (ghi vào D55 mục 4).
  - `frontend/.dockerignore` (trước đó chép cả `node_modules` của máy dev), tạo `public/` rỗng lúc build.
  - Log Serilog của máy dev (có email người dùng) bị `dotnet publish` chép vào image → loại `**/logs/` trong
    `.dockerignore`; tạo `/app/logs` cho user `app` sau `COPY` để File sink ghi được.
  - Bỏ `ASPNETCORE_URLS` trong Dockerfile API (trùng `ASPNETCORE_HTTP_PORTS` có sẵn, sinh cảnh báo).
- Bước 7 của smoke test không upload qua API được (cần recipe — FR-RCP-003 chưa có): ghi thẳng ảnh PNG vào
  bucket, kiểm tra GET 200 / PUT 403 / `next/image` 200.
- `verify-prod.sh` thêm bước 9: API ghi được log file, image không mang log của máy dev.
- Kết quả: 18 test mới pass · `dotnet test` toàn bộ pass (267 unit + 259 integration + 10 architecture) ·
  `npm run lint && npm run build` pass · Jest 340/340 · `verify-prod.sh` 16/16 · backup/giữ 30 ngày kiểm tay OK.
