# Changelog

Theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/) và [SemVer](https://semver.org/lang/vi/)
(NFR-MAINT-003).

## [Unreleased]

### Added
- Khung dự án Sprint 0: solution 4 tầng Clean Architecture + 3 project test,
  MediatR pipeline (4 behavior), GlobalExceptionMiddleware theo RFC 7807,
  CorrelationIdMiddleware, health check 3 endpoint, cache Redis tag-based.
- 30 endpoint khai báo sẵn (trả 501) để Scalar UI hiển thị đầy đủ API contract từ ngày đầu.
- Frontend Next.js 16 App Router + Tailwind 4 + TanStack Query + design token.
- Hạ tầng dev: docker-compose (postgres, redis, minio, seq, mailhog), Nginx, GitHub Actions CI.
- Architecture test kiểm tra Dependency Rule (CONS-001).
- Tài liệu: `docs/decisions.md` (22 quyết định chốt mâu thuẫn SRS), `traceability.md`,
  `permissions.md`, `roadmap.md`, `team-assignment.md`.
- FR-RCP-008: quản lý ảnh công thức — 3 endpoint `POST /recipes/{id}/images` (multipart),
  `PATCH .../images/{imageId}` (`altText`, `isPrimary`, `orderIndex`), `DELETE .../images/{imageId}` (D22, D27).
  Ảnh đầu tiên tự động primary; xóa ảnh primary thì ảnh kế lên thay. Validation size → MIME → magic bytes
  (JPEG/PNG/WebP/AVIF, D28); upload/đổi primary đồng thời an toàn (khóa dòng Recipe). Frontend: component
  upload + thanh tiến độ, gallery kéo-thả sắp xếp (cả bàn phím), đặt ảnh chính, xóa (`components/upload/`).
- FR-FILE-001/002: `IFileStorageService` + `MinioFileStorageService` (AWSSDK.S3, path-style) — key
  `recipes/{recipeId}/{Guid}{đuôi theo magic bytes}`, bucket đọc công khai (D16). Xóa ảnh xóa file qua Hangfire
  (gồm cả biến thể medium/thumbnail — D43), idempotent; xóa recipe là soft delete, không xóa file (D1).
- FR-JOB-002: `ResizeRecipeImageJob` (Hangfire, retry 3 lần) sinh biến thể WebP medium 800×600 và thumbnail
  300×300 bằng SkiaSharp sau upload (D40–D46): không phóng to, bỏ EXIF, giới hạn 40 megapixel.
- FR-OBS-001: health check `/health` (Postgres + Redis + S3), `/health/live`, `/health/ready` (Postgres + Redis).
- FR-OBS-002: Serilog structured log — `CorrelationId` (`X-Correlation-ID`), `RequestPath`, `UserId`; cảnh báo
  request/handler chậm > 500ms; sink Console JSON + file rolling + Seq (dev).
- FR-OBS-003 (một phần): OpenTelemetry trace (ASP.NET Core, HttpClient, Npgsql) + metric HTTP, export OTLP khi
  có endpoint (D36–D38). Metric `CulinaryBlog.Recipes` (D39) chờ handler tạo/publish công thức (FR-RCP-003/005).
- FR-JOB-003 / NFR-SEO-003: Hangfire recurring `SitemapGenerationJob` (02:00 UTC, retry 2 lần,
  `DisableConcurrentExecution`) sinh `sitemap.xml` (trang tĩnh + category + recipe Published) lên MinIO
  `seo/sitemap.xml`; Nginx phục vụ `/sitemap.xml`; `frontend/app/robots.ts`. Cấu hình mới `Seo:SiteBaseUrl`.
  `IFileStorageService.SaveAsync` cho file hệ thống sinh. Không ping Google (D51–D53).
- CONS-009: `docker-compose.prod.yml` — chỉ Nginx publish 80/443 (D60), HTTPS TLS 1.2+ với HTTP → 301 và
  HSTS 1 năm (`nginx/nginx.prod.conf`, NFR-SEC-005), service `migrate` chạy `--migrate-only` trước API (D59),
  ảnh MinIO phục vụ qua Nginx `/culinary-blog/` với cấu hình mới `Minio:PublicBaseUrl` (D58), sao lưu
  `pg_dump` 03:00 UTC giữ 30 ngày (`scripts/pg-backup.sh`, NFR-REL-003), smoke test `scripts/verify-prod.sh`.
  Hướng dẫn: `docs/deployment.md`. API bật `UseForwardedHeaders`; frontend build `output: 'standalone'`.
- NFR-SEO-001/002: JSON-LD Schema.org Recipe đầy đủ (`datePublished`, ảnh tuyệt đối, `HowToStep` neo `#buoc-n`)
  qua `components/seo/RecipeJsonLd.tsx`; meta description 150–160 ký tự, canonical, Open Graph, Twitter
  `summary_large_image`; og:image 1200×630 sinh tại `/recipes/{slug}/og-image` (ảnh chính hoặc thẻ chữ);
  `metadataBase` = `NEXT_PUBLIC_SITE_URL`; canonical không query param; `/search` `noindex` (D61).
- NFR-PERF-005: Lighthouse CI (`frontend/lighthouserc.json`, `npm run lhci`, job CI `lighthouse`) đo 6 trang
  public, preset mobile, trung vị 3 lần: LCP ≤ 2500ms, CLS ≤ 0.1, TBT ≤ 200ms (thay INP), JS ≤ 200KB (D64).

### Changed
- NFR-PERF-005: JS mỗi trang public giảm 20KB — axios không còn bị tải trên trang public
  (`lib/problem-details.ts`). Bỏ skeleton `loading.tsx` của `/` và `/categories` (làm trễ LCP — D64.6).
- FR-CAT-005: nút xóa danh mục trên `/categories` chỉ hiện với Admin (trước đây hiện cả với Guest).
- CONS-009: MinIO đã gỡ mọi image công khai → S3 dev/CI dùng `adobe/s3mock` (D65), S3 production dùng
  RustFS 1.0.3, bucket + policy chỉ-GET-ẩn-danh tạo bằng `amazon/aws-cli` (D66). Giữ tên service `minio`,
  cổng 9000 và biến `MINIO_ROOT_*`; image ghi đè qua `S3_IMAGE` (bỏ `MINIO_IMAGE`/`MC_IMAGE`).

### Fixed
- NFR-SEO-002: title trang chi tiết công thức bị lặp đuôi `| Culinary Blog | Culinary Blog`.
- FR-CAT-001: `/categories` thành SSR khi build image production không có API — nay giữ ISR 3600.
- CONS-005: response lỗi của API trả `Content-Type: application/problem+json` (trước đây bị ghi đè thành
  `application/json`). Lỗi bind request (JSON hỏng, thiếu tham số) trả RFC 7807 thay vì 400 rỗng / 500 (D67).
- CONS-009: `scripts/verify-prod.sh` bước 9 báo lỗi sai khi volume `apilogs` còn log của lần chạy trước —
  nay kiểm thẳng image (container tạm không gắn volume).

### Security
- NFR-SEC-004: upload ảnh giới hạn 6MB ở tầng HTTP — vượt → 413 `FILE_SIZE_EXCEEDED`, không nhận hết body (D67).
  Thêm Content-Security-Policy cho trang web và API (D68). Test chặn hồi quy SQL injection, path traversal, raw SQL.
- NFR-SEC-007: bỏ `Jwt:Key` khỏi `appsettings.json` và khóa dự phòng hardcode trong `Program.cs`. API dừng
  ngay khi thiếu khóa hoặc khóa < 32 byte. **Khóa cũ vẫn còn trong lịch sử Git — production phải dùng khóa mới.**
