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
- FR-AUTH-001→007: đăng ký (tự đăng nhập), đăng nhập email/mật khẩu, đăng nhập Google qua ID Token (chặn
  chiếm tài khoản qua email chưa xác minh; liên kết email trùng), làm mới token có rotation + phát hiện dùng lại
  (D20), đăng xuất thu hồi refresh token, xem / cập nhật hồ sơ `PATCH /auth/me` (`displayName`, `avatarUrl`,
  `bio` — D5, D54). Frontend Auth.js v5: `/auth/register`, `/auth/login`, nút Google, tự làm mới access token,
  gắn Bearer vào `apiClient` (D74), chặn `/dashboard/*` khi chưa đăng nhập, `/dashboard/profile` (D62).
- FR-CAT-001→005: danh sách danh mục (đếm recipe đã xuất bản), chi tiết danh mục kèm công thức, tạo / sửa / xóa
  mềm danh mục cho Admin (còn công thức → 409 `CATEGORY_DELETE_HAS_RECIPES`; tên trùng → `CATEGORY_NAME_EXISTS`;
  slug tự thêm hậu tố — D10). Trang `/categories` (ISR 3600), `/categories/[slug]` (ISR 600), quản trị danh mục.
- FR-RCP-001/002: `GET /recipes` (lọc, sắp xếp, phân trang) và chi tiết theo slug (Draft/Archived chỉ tác giả
  hoặc Admin). Trang chủ `/` (ISR 3600), `/recipes` (SSR), `/recipes/[slug]` (ISR 300, 404 thật cho slug không
  tồn tại). "Công thức của tôi" `?mine=true&status=` + `/dashboard/recipes` (D81), tổng quan Author `/dashboard`
  và link "Bảng điều khiển" trên header (D82).
- FR-RCP-003→007, 009, 010: tạo công thức (wizard 4 bước), sửa thông tin chung với `If-Match` / `ETag` (D71),
  CRUD bước (tự đánh số lại — D73) và nguyên liệu (`quantity`/`unit` có thể trống — D76), xuất bản / hủy xuất
  bản (cần ≥ 1 bước và ≥ 1 nguyên liệu — D3, D77), lưu trữ (D79), xóa mềm (D78). Đổi trạng thái thì xóa cache
  ISR của Next.js ngay (D80). `GET /recipes/{id}` cho trang sửa (D75).
- FR-SRCH-001→004: tìm kiếm toàn văn tiếng Việt (`SearchVector` cập nhật bằng trigger, GIN index, xếp theo
  `ts_rank`, tsquery được làm sạch — D49), lọc theo danh mục / độ khó / thời gian nấu / khẩu phần, sắp xếp,
  phân trang (tối đa 50). Trang `/search` (SSR, `noindex`).
- FR-JOB-001: email chào mừng HTML qua Hangfire (retry 3 lần 1/5/30 phút) khi đăng ký và khi đăng nhập Google
  lần đầu (D55–D57); lỗi enqueue không làm hỏng request.
- NFR-SEC-003: rate limit theo IP (`/auth/*` 10 request/phút — D15), đếm đúng IP người dùng khi request đi qua
  Next.js (D63). NFR-SEC-005: CORS allowlist. NFR-SEC-007: hook pre-commit gitleaks (`.githooks/`).
- NFR-USE-001/002: responsive theo breakpoint 320/768/1200px, vùng bấm ≥ 24px (D84); kiểm WCAG 2.1 AA bằng
  axe-core trong E2E trên 16 route (D83). NFR-USE-004: optimistic update + hoàn tác khi lỗi cho đổi chỗ / xóa
  bước, nguyên liệu và cho đặt ảnh chính / xóa ảnh (dialog xóa ảnh đóng ngay).
- NFR-PERF-001→004: script k6 smoke / load / stress, đo tỉ lệ cache hit Redis, `EXPLAIN ANALYZE` các query chính
  (`backend/tests/performance/`).
- NFR-MAINT-003: README hướng dẫn chạy môi trường dev trong 5 phút (đo trên bản clone mới: 145 giây).
- Dữ liệu seed dev: ảnh thật cho món ăn (Wikimedia Commons).
- FR-RCP-008: quản lý ảnh công thức — 3 endpoint `POST /recipes/{id}/images` (multipart),
  `PATCH .../images/{imageId}` (`altText`, `isPrimary`, `orderIndex`), `DELETE .../images/{imageId}` (D22, D27).
  Ảnh đầu tiên tự động primary; xóa ảnh primary thì ảnh kế lên thay. Validation size → MIME → magic bytes
  (JPEG/PNG/WebP/AVIF, D28); upload/đổi primary đồng thời an toàn (khóa dòng Recipe). Frontend: component
  upload + thanh tiến độ, gallery kéo-thả sắp xếp (cả bàn phím), đặt ảnh chính, xóa (`components/upload/`).
- FR-FILE-001/002: `IFileStorageService` + `MinioFileStorageService` (AWSSDK.S3, path-style) — key
  `recipes/{recipeId}/{Guid}{đuôi theo magic bytes}`, bucket đọc công khai (D16). Xóa ảnh xóa file qua Hangfire
  (gồm cả biến thể medium/thumbnail — D43), idempotent; xóa recipe là soft delete, không xóa file (D1).
- FR-JOB-002: `ResizeRecipeImageJob` (Hangfire, retry 3 lần) sinh biến thể WebP medium 800×600 và thumbnail
  300×300 bằng SkiaSharp sau upload (D40–D46): không phóng to, bỏ EXIF, giới hạn 40 megapixel. Thêm biến thể
  **card 760×320** cắt đúng khung thẻ công thức (cột `RecipeImage.CardUrl`, migration `AddRecipeImageCardUrl` — D88).
- FR-OBS-001: health check `/health` (Postgres + Redis + S3), `/health/live`, `/health/ready` (Postgres + Redis).
- FR-OBS-002: Serilog structured log — `CorrelationId` (`X-Correlation-ID`), `RequestPath`, `UserId`; cảnh báo
  request/handler chậm > 500ms; sink Console JSON + file rolling + Seq (dev).
- FR-OBS-003: OpenTelemetry trace (ASP.NET Core, HttpClient, Npgsql) + metric HTTP, export OTLP khi có endpoint
  (D36–D38); `TraceId` trong log; metric `CulinaryBlog.Recipes` đếm công thức tạo/xuất bản (D39).
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
  public, preset mobile, trung vị 9 lần đo (D87): LCP ≤ 2500ms, CLS ≤ 0.1, TBT ≤ 200ms (thay INP), JS ≤ 200KB (D64).

### Changed
- D97: giao diện "sổ tay nấu ăn" tông tối — nền nâu than, chữ kem, màu chính cam đất (token mới `on-brand`, `accent`,
  `danger`/`success`/`warning`, mọi cặp màu ≥ 4.5:1); font Baloo 2 (tiêu đề) + Be Vietnam Pro qua `next/font`; icon
  niêu đất (favicon, logo). Header ngang → thanh bên Mục lục (danh mục đánh số, ô tìm kiếm chung). Trang chủ: Món
  đáng thử + Thực đơn mới (đường chấm dẫn). Trang chi tiết: dòng thời gian nấu theo phút (cộng dồn thời lượng
  bước), nguyên liệu dạng danh sách đi chợ có ô tick. Thời gian ≥ 60 phút hiện theo giờ.
- NFR-PERF-005: `/search` không stream kết quả qua Suspense (kết quả nằm sẵn trong HTML đầu tiên); preload ảnh
  thẻ đầu tiên; ảnh thẻ nhẹ hơn (quality 50, biến thể card); làm nóng cache trước khi đo (D86). LCP `/search`
  2.6–3.0s → 2486ms.
- NFR-PERF-005: JS mỗi trang public giảm 20KB — axios không còn bị tải trên trang public
  (`lib/problem-details.ts`). Bỏ skeleton `loading.tsx` của `/` và `/categories` (làm trễ LCP — D64.6).
- FR-CAT-005: nút xóa danh mục trên `/categories` chỉ hiện với Admin (trước đây hiện cả với Guest).
- CONS-009: MinIO đã gỡ mọi image công khai → S3 dev/CI dùng `adobe/s3mock` (D65), S3 production dùng
  RustFS 1.0.3, bucket + policy chỉ-GET-ẩn-danh tạo bằng `amazon/aws-cli` (D66). Giữ tên service `minio`,
  cổng 9000 và biến `MINIO_ROOT_*`; image ghi đè qua `S3_IMAGE` (bỏ `MINIO_IMAGE`/`MC_IMAGE`).

### Fixed
- FR-RCP-002: độ khó ở trang chi tiết lệch một bậc (đọc 0/1/2 thay vì enum Easy = 1 … Expert = 4); khung "Giá trị
  dinh dưỡng" trống khi mọi chỉ số rỗng.
- FR-RCP-008: upload ảnh từ giao diện luôn bị 415 — axios biến `FormData` thành JSON vì `apiClient` mặc định
  `Content-Type: application/json`. Nay gửi đúng multipart.
- FR-RCP-003: tạo hai công thức cùng tiêu đề đồng thời không còn trả 500 — slug thứ hai tự thêm hậu tố (D85).
- NFR-USE-002: giữ focus sau khi đổi chỗ bước / nguyên liệu; thứ bậc heading ở `/recipes`, `/search`; toast được
  trình đọc màn hình đọc và đủ tương phản; màu thương hiệu đậm hơn để chữ trắng đạt 4.5:1.
- FR-AUTH-003: Google không trả tên → `displayName` lấy phần trước `@` của email (D56).
- D34/D4: `RowVersion` thật sự chặn ghi đè đồng thời trên PostgreSQL (409 `RECIPE_CONCURRENCY_CONFLICT`).
- NFR-SEO-002: title trang chi tiết công thức bị lặp đuôi `| Culinary Blog | Culinary Blog`.
- FR-CAT-001: `/categories` thành SSR khi build image production không có API — nay giữ ISR 3600.
- CONS-005: response lỗi của API trả `Content-Type: application/problem+json` (trước đây bị ghi đè thành
  `application/json`). Lỗi bind request (JSON hỏng, thiếu tham số) trả RFC 7807 thay vì 400 rỗng / 500 (D67).
- CONS-009: `scripts/verify-prod.sh` bước 9 báo lỗi sai khi volume `apilogs` còn log của lần chạy trước —
  nay kiểm thẳng image (container tạm không gắn volume).
- CONS-008: `ValidationBehavior` báo mỗi lỗi hai lần khi một request có từ 2 validator (dùng chung
  `ValidationContext`), và chạy validator song song trên cùng `DbContext`. Nay mỗi validator một context, chạy
  tuần tự. Chưa ảnh hưởng request nào (hiện mỗi command chỉ có một validator) — phát hiện bằng unit test
  NFR-MAINT-002.

### Security
- NFR-SEC-004: upload ảnh giới hạn 6MB ở tầng HTTP — vượt → 413 `FILE_SIZE_EXCEEDED`, không nhận hết body (D67).
  Thêm Content-Security-Policy cho trang web và API (D68). Test chặn hồi quy SQL injection, path traversal, raw SQL.
- NFR-SEC-007: bỏ `Jwt:Key` khỏi `appsettings.json` và khóa dự phòng hardcode trong `Program.cs`. API dừng
  ngay khi thiếu khóa hoặc khóa < 32 byte. **Khóa cũ vẫn còn trong lịch sử Git — production phải dùng khóa mới.**
