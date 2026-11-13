# Quyết định Thiết kế đã chốt — Culinary Blog v1.0.0

SRS v1.0.0 có 22 chỗ tự mâu thuẫn hoặc thiếu thông tin. Tài liệu này **chốt cả 22**.

- **Ngày chốt:** 2026-09-15 · **Người quyết định:** nhóm phát triển
- **Hiệu lực:** những quyết định dưới đây **thắng SRS** ở mọi chỗ mâu thuẫn.
- Khi code, thứ tự ưu tiên là: `decisions.md` → `docs/SRS.md` → suy đoán (không được phép).

> Không xóa các quyết định đã chốt — nếu đổi ý, thêm dòng
> "**ĐÃ SỬA ĐỔI** ngày … vì …" bên dưới và giữ lại quyết định cũ. Sáu tháng nữa
> bạn sẽ cần biết vì sao đã làm thế.

---

## Bảng tra nhanh

| # | Chủ đề | Quyết định |
|---|---|---|
| D1 | Xóa Recipe | **Soft delete** (`IsDeleted = true`) |
| D2 | Xóa Category | **Soft delete** |
| D3 | Điều kiện publish | Cần **≥ 1 step VÀ ≥ 1 ingredient** |
| D4 | Mã lỗi validation / concurrency | **400** / **409** |
| D5 | Trường hồ sơ user | `displayName`, `avatarUrl`, `bio` |
| D6 | RecipeStep | `title` bắt buộc, `timerMinutes`, **server sinh** `stepNumber` |
| D7 | RecipeIngredient | `quantity`/`unit` **nullable**, `orderIndex`, name ≤ 200 |
| D8 | Cache | **Chỉ Redis** qua `CachingBehavior` |
| D9 | Google OAuth | `POST /auth/google` body `{ idToken }` |
| D10 | Slug trùng | **Auto-suffix** `-2`, `-3` — không trả 409 |
| D11 | Admin quản lý user | **Ngoài scope v1** |
| D12 | Email verification | **Ngoài scope v1**, bỏ policy `VerifiedAuthor` |
| D13 | 301 redirect khi đổi slug | **Bỏ khỏi v1** |
| D14 | Filter `minServings` | **Có** hỗ trợ |
| D15 | Rate limit | Theo NFR-SEC-003: 10 / 100 / 5 req/phút/IP |
| D16 | Bucket MinIO | Giữ **public-read**, chấp nhận rủi ro |
| D17 | Tài khoản bị khóa | **423** + `AUTH_ACCOUNT_LOCKED` |
| D18 | `Recipe.Instructions` | Đổi sang **NULL** |
| D19 | `CookTime` | Cho phép **0** |
| D20 | RefreshToken schema | Theo mô hình 7.8, `IsRevoked` là computed |
| D21 | Tổng số FR | **34** |
| D22 | Endpoint sửa metadata ảnh | `PATCH /recipes/{id}/images/{imageId}` |
| D23 | Vị trí `ApplicationUser` | **Infrastructure**, không phải Domain — qua `IIdentityService` (ADR-0003) |
| D24 | Response `POST /auth/register` | **Đầy đủ `AuthResponseDto`** (auto-login), không phải `{userId,email,displayName}` |
| D25 | Độ dài refresh token | **128-bit** (theo NFR-SEC-002), không phải 512-bit |
| D26 | Vị trí `RefreshToken` | **Domain**, không phải Infrastructure — POCO thuần, không như `ApplicationUser` |
| D27 | Hợp đồng dữ liệu ảnh (FR-RCP-008) | Response 201 theo 8.4 · `orderIndex` = Max+1 · PATCH rỗng → 400 · mọi status đều sửa được ảnh |
| D28 | Magic bytes upload | JPEG · PNG · WebP · AVIF — đuôi file lấy từ định dạng phát hiện được |
| D29 | Bug: role hệ thống chưa seed | `IdentityRoleSeeder` — thiếu thì register/login trả 500 |
| D30 | Bug: mất ErrorCode của FluentValidation | `GlobalExceptionMiddleware` phải giữ ErrorCode riêng của từng rule |
| D31 | Bug: thêm child entity vào aggregate đã track | EF Core hiểu nhầm thành UPDATE thay vì INSERT |
| D32 | Bug: `CategoryRepository.GetBySlugAsync` thiếu Include | `category.Recipes` luôn rỗng — FR-CAT-002 không hoạt động |
| D33 | Google OAuth lỗi | A1+A2 → 400 `AUTH_GOOGLE_TOKEN_INVALID`, A3 → 502 `AUTH_GOOGLE_UNAVAILABLE` |
| D34 | `RowVersion` trên PostgreSQL | **Code tự sinh** qua `RowVersionInterceptor` — `[Timestamp]` chỉ tự chạy trên SQL Server |
| D35 | Refresh token (FR-AUTH-004) | A1/A4 → 401 `AUTH_TOKEN_INVALID` · không check lockout · reuse → revoke **mọi** RT của user · race → 401 không revoke family |
| D36 | Đích export OpenTelemetry | Dev: **trace → Seq** (OTLP HTTP), metric không export · Prod: cả hai → Collector |
| D37 | "EF Core traces" | **`Npgsql.OpenTelemetry`** (span theo câu SQL), không dùng gói EF Core beta |
| D38 | Error rate | **5xx / exception** (`error.type`) — 4xx không tính lỗi server |
| D39 | Metric recipe created/published | `RecipeMetrics` làm trước; **C gọi** trong handler FR-RCP-003/005 |
| D40 | Cách resize (FR-JOB-002) | Thumbnail **crop giữa** 300×300 · medium **fit** trong 800×600 · không upscale · bỏ EXIF |
| D41 | Định dạng ảnh phái sinh | Luôn **WebP** · ảnh gốc **AVIF → bỏ qua** (ImageSharp không decode được) |
| D42 | Retry FR-JOB-002 | 3 lần, chờ **1 / 5 / 30 phút** (giống FR-JOB-001) |
| D43 | Xóa ảnh | Xóa cả **original + medium + thumbnail** trên MinIO |
| D44 | Luồng job resize | Job → MediatR command `GenerateRecipeImageVariantsCommand` · idempotent |
| D45 | Hangfire server | **Bật lại** `AddHangfireServer()` (trừ môi trường `Testing`) |
| D46 | Thư viện resize ảnh | **SkiaSharp** thay ImageSharp (lỗ hổng 3.1.12 · 4.x cần license) — xem ADR-0004 |
| D47 | Logout (FR-AUTH-005) | Giữ `RequireAuthorization` · thiếu `refreshToken` → 400 · token user khác / đã revoke / hết hạn → **204 no-op** · không kích hoạt reuse detection |
| D48 | Xem hồ sơ (FR-AUTH-006) | User bị xóa sau khi cấp token → **404** `USER_NOT_FOUND` · user `IsActive=false` còn access token → **200** (chỉ đọc) |
| D49 | Full-text search (FR-SRCH-001) | `simple` + `unaccent` (không có cấu hình `vietnamese`) · `SearchVector` do **trigger** cập nhật + GIN index · Title trọng số A, Description B · xếp `ts_rank` · bỏ khớp `ILIKE` giữa từ |
| D50 | Non-Admin gọi lệnh ghi Category (NFR-SEC-006) | Handler kiểm tra lại `IsAdmin` → **403** `CATEGORY_FORBIDDEN` (thêm mã mới) |
| D51 | Ping Google sau khi sinh sitemap (FR-JOB-003) | **Bỏ** — endpoint đã ngừng; khai báo qua `Sitemap:` trong robots.txt |
| D52 | Nơi lưu & phục vụ sitemap.xml | **MinIO** key cố định `seo/sitemap.xml` + Nginx proxy `/sitemap.xml` · bỏ `frontend/app/sitemap.ts` |
| D53 | Distributed lock cho sitemap job | Hangfire **`DisableConcurrentExecution`** thay RedLock |
| D54 | Cập nhật hồ sơ (FR-AUTH-007) | Body rỗng → 400 · field vắng/`null` = giữ nguyên, `""` = xóa (`bio`, `avatarUrl`) · `IsActive=false` → 200 · user đã xóa → 404 |
| D55 | Enqueue welcome email lỗi (FR-JOB-001) | **Log Warning, không fail request** — register/Google login vẫn trả token |
| D56 | Ai nhận welcome email | Đăng ký (FR-AUTH-001) **và** tài khoản Google **mới** (FR-AUTH-003) · Google liên kết tài khoản có sẵn → không gửi |
| D57 | Nội dung & cấu hình welcome email | Link `/` + `/dashboard` từ `Seo:SiteBaseUrl` (thiếu → job lỗi) · HTML-encode `displayName` · multipart HTML + text · SMTP auth khi có `Smtp:Username` |
| D58 | URL công khai của ảnh MinIO (CONS-009) | Cấu hình `Minio:PublicBaseUrl` (rỗng → dùng `Minio:Endpoint`) · prod = `https://{domain}`, Nginx proxy `/culinary-blog/` (GET/HEAD) tới MinIO |
| D59 | Migration + seed role ở prod (CONS-009) | Service `migrate` một lần: `--migrate-only` = migrate + `IdentityRoleSeeder` rồi thoát · **không** chạy `DbSeeder` · `api` chờ `service_completed_successfully` |
| D60 | Cổng prod (CONS-009, NFR-SEC-005) | **Chỉ Nginx** publish 80/443 (lệch bảng SRS §6.5) · API bật `ForwardedHeaders`, tin proxy trong mạng nội bộ |
| D61 | og:image, độ dài description, "pass 100%" (NFR-SEO-001/002) | og:image 1200×630 sinh bằng Route Handler `/recipes/{slug}/og-image` (fallback thẻ chữ) · description bổ sung/cắt về 150–160 · Rich Results = **0 lỗi** · `/search` + `/auth/*` `noindex` · canonical không query param |
| D62 | Route trang hồ sơ & chặn route chưa đăng nhập (FR-AUTH-006/007) | Trang hồ sơ ở **`/dashboard/profile`**, không có `/profile` · `proxy.ts` chặn `/dashboard/*` khi chưa đăng nhập → `/auth/login?callbackUrl=<đường dẫn nội bộ>` · chỉ để UX, backend vẫn tự kiểm tra |
| D63 | IP để rate limit khi request đi qua Next.js (NFR-SEC-003) | Server Next.js gửi `X-Forwarded-For` = `X-Real-IP` do Nginx ghi · Nginx đặt `proxy_set_header` ở mức server (location `/api/` không khai báo riêng) · Dev ngưỡng cao |
| D69 | Hợp đồng request tạo/sửa recipe (FR-RCP-003/004) | `prepTime`/`cookTime` (Chương 8) · `difficulty` chữ hoặc số 1–4 · category sai → **400** · PUT thay toàn bộ, `nutrition` null = giữ nguyên |
| D70 | Phân quyền Recipe ở Application | `RecipeAuthorizationHandler` dùng `ICurrentUser` (không `IAuthorizationService`) — ADR-0005 |
| D71 | `If-Match`/`RowVersion` (FR-RCP-004) | DTO có `rowVersion` + header `ETag` · PUT thiếu `If-Match` → **400** · lệch → **409** |
| D72 | Chi tiết slug recipe | Gộp `-`, cắt 200, rỗng → `cong-thuc`, slug cấm thêm `-1`, trùng (kể cả đã xóa mềm) → `-2`… |
| D73 | Xóa / đánh số lại bước (FR-RCP-010) | **Hard delete** · lưu **2 pha** + khóa dòng recipe · `RECIPE_STEP_NOT_FOUND` (404) · stepNumber ngoài khoảng → 400 · không xóa bước cuối của recipe Published |
| D74 | Token cho lời gọi API từ trình duyệt | Interceptor `apiClient` gắn Bearer từ `getSession()` (Auth.js tự refresh) · cache 30s |
| D75 | Trang sửa đọc recipe theo id | `GET /recipes/{id:guid}` không cache, quyền như FR-RCP-002 · slug dạng GUID thêm `-1` |
| D76 | Nguyên liệu (FR-RCP-009) | **Hard delete** · `orderIndex` 0-based, vắng = cuối, server giữ liên tục · `RECIPE_INGREDIENT_NOT_FOUND` (404) · ngoài khoảng → 400 · không xóa nguyên liệu cuối của recipe Published · khóa dòng, lưu 1 lần |
| D77 | Publish / Unpublish (FR-RCP-005) | Archived publish/unpublish được · đã ở trạng thái đích → 200 không ghi · `PublishedAt` = lần đầu · `If-Match` **tùy chọn**, lệch → 409 · trả `RecipeDetailDto` + `ETag` · khóa dòng |
| D78 | Xóa recipe (FR-RCP-007) | Soft delete, con không bị đụng · mọi trạng thái xóa được · sau khi xóa mọi thao tác (kể cả DELETE lần 2) → **404** · `If-Match` tùy chọn · khóa dòng |
| D79 | Archive recipe (FR-RCP-006) | Từ Draft/Published · đã Archived → 200 không ghi · `If-Match` tùy chọn · trả `RecipeDetailDto` + `ETag` · khóa dòng · giữ `PublishedAt` |
| D80 | Xóa cache Next sau khi đổi trạng thái recipe | Server Action `revalidateRecipeAction(slug)` → `updateTag('recipes')` + `updateTag('recipe:{slug}')` sau publish/unpublish/archive/delete · fetch trang chủ + trang danh mục gắn tag `recipes` |
| D81 | Danh sách "công thức của tôi" (`/dashboard/recipes`) | `GET /recipes?mine=true` (chưa đăng nhập → 401) + lọc `status` sau bộ lọc quyền · không route `/recipes/mine` · Admin cũng chỉ thấy bài của mình · DTO giữ nguyên |
| D82 | Trang tổng quan Author (`/dashboard`) | 4 ô số Tổng/Nháp/Đã xuất bản/Đã lưu trữ (link sang `/dashboard/recipes?status=`) + 5 bài mới nhất · dùng 4 lời gọi `GET /recipes?mine=true` có sẵn, **không thêm endpoint** · chỉ Author/Admin · lỗi chỉ hỏng phần tổng quan |
| D83 | Cách đo a11y (NFR-USE-002) | Screen reader **chỉ NVDA** (nhóm không có Mac/iPhone), VoiceOver "chưa kiểm" · axe-core trong E2E (0 vi phạm WCAG 2.1 A/AA) + checklist tay `docs/a11y-checklist.md` |
| D84 | Responsive (NFR-USE-001) | Breakpoint `lg` = **1200px** (Tailwind mặc định 1024) · touch-friendly = vùng bấm ≥ 24×24px (WCAG 2.5.8) · bảng dữ liệu cuộn ngang trong khung riêng · không có BrowserStack → Playwright giả lập, thiết bị thật "chưa kiểm" |
| D85 | Hai request tạo recipe cùng tiêu đề đến cùng lúc (FR-RCP-003) | Lỗi trùng `IX_Recipes_Slug` → sinh lại slug và thử lại, **tối đa 5 lần** · không khóa bảng · hết 5 lần → 500 |
| D86 | LCP `/search` + cách đo Lighthouse CI (NFR-PERF-005) | `/search` **không** stream kết quả qua `<Suspense>` (kết quả có ngay trong HTML đầu) · báo đang tải bằng transition (`aria-busy`) · làm nóng cache trước khi đo · ngưỡng giữ 2500ms |
| D87 | Lighthouse CI chấm theo trung vị (NFR-PERF-005) | `aggregationMethod: "median"` — trung vị **9** lần đo cho từng chỉ số, thay `median-run` (chấm 1 lần "đại diện") · ngưỡng giữ nguyên |
| D88 | Biến thể ảnh `card` (FR-JOB-002, NFR-PERF-005) | Thêm biến thể thứ ba: **crop giữa 760×320** WebP, không phóng to · `RecipeImage.CardUrl` · `RecipeSummaryDto.FeaturedImageCardUrl` · card dùng ảnh card, chưa có thì ảnh gốc · ảnh trước D88: job chỉ bổ sung card |
| D90 | Redis tắt / lên lại (NFR-REL-002) | `BacklogPolicy.FailFast` + timeout lệnh 500 ms — mất kết nối thì đi thẳng xuống DB ngay, không chờ 5 s · kết nối lại → xóa **mọi** key cache theo `tag:*` (không trả bản cũ do lệnh ghi lúc Redis tắt) |
| D91 | Cookie SameSite (NFR-SEC-005) | Cookie Auth.js giữ **`Lax`**, không `Strict` — `Strict` làm hỏng đăng nhập Google (cookie state/PKCE không được gửi khi Google redirect về). API dùng Bearer token nên không có CSRF qua cookie |
| D92 | Audit log mọi write (NFR-SEC-006) | v1 = **request log có cấu trúc** (FR-OBS-002: method, path, status, `UserId`, `CorrelationId`, thời gian) cho mọi POST/PUT/PATCH/DELETE + log riêng `{RecipeId}`/`{UserId}` cho xóa/publish/archive. Không có bảng AuditLog |
| D93 | Khóa JWT cũ trong lịch sử Git (NFR-SEC-007) | Không viết lại lịch sử Git. Khóa cũ coi như đã lộ, chỉ dùng dev; production **bắt buộc** sinh khóa mới (`openssl rand -base64 48`, `docs/deployment.md`) |
| D94 | CDN (NFR-SCALE-003) | Ngoài phạm vi v1 (không có domain/ngân sách). Tài nguyên tĩnh đã sẵn sàng cho CDN: `/_next/static` immutable, ảnh tên GUID `expires 30d` — gắn CDN sau chỉ là cấu hình DNS, không đổi code |
| D95 | "2 vCPU / 4 GB" (NFR-PERF-002) | Hiểu là giới hạn của **một instance API**; PostgreSQL / Redis là service riêng (NFR-SCALE-003: mỗi service 1 container). Đo toàn bộ stack trong 2 vCPU chưa làm |
| D96 | SonarAnalyzer / StyleCop, Prettier (NFR-MAINT-001) | Dùng analyzer có sẵn của .NET (`AnalysisLevel=latest`) + `TreatWarningsAsErrors`, không thêm SonarAnalyzer/StyleCop. Prettier: có cấu hình, CI **chưa** kiểm (59 file chưa format) — format + thêm `prettier --check` sau demo |

---

## D1 — Xóa Recipe: soft delete

**Chốt:** `DELETE /api/v1/recipes/{id}` đặt `IsDeleted = true`, không xóa vật lý.

**Vì:** NFR-REL-003, Chương 8 và Phụ lục A đều nói soft delete (3/4 nguồn), và `BaseEntity`
đã có sẵn `IsDeleted` + Global Query Filter cho mọi entity. FR-RCP-007 là chỗ duy nhất nói
hard delete — SRS sai ở đó.

**Hệ quả cụ thể:**

- Không cascade delete. `RecipeStep`, `RecipeIngredient`, `RecipeImage` **giữ nguyên** trong DB.
  Chúng biến mất khỏi mọi query vì luôn được truy cập qua Recipe cha đã bị lọc.
- **Không có Hangfire job xóa file MinIO khi xóa recipe.** Ảnh vẫn nằm trên MinIO
  (recipe còn có thể khôi phục). FR-FILE-002 chỉ dùng cho `DELETE /recipes/{id}/images/{imageId}`
  — xóa một ảnh cụ thể.
- **Slug KHÔNG được tái sử dụng.** Unique index trên `Recipes.Slug` áp cho cả bản ghi đã xóa.
  Tạo recipe mới trùng title → auto-suffix (xem D10).
- **Không có endpoint khôi phục trong v1.** Cần khôi phục thì sửa trực tiếp DB.
- **Sửa SRS:** FR-RCP-007 — đổi "hard delete, cascade delete" thành soft delete, bỏ bước 5
  (Hangfire xóa file MinIO).

---

## D2 — Xóa Category: soft delete

**Chốt:** `DELETE /api/v1/categories/{id}` đặt `IsDeleted = true`.

**Vì:** nhất quán với D1 và với Chương 8.

**Hệ quả:**

- FK `Recipes.CategoryId` giữ `ON DELETE RESTRICT`, nhưng nó **không bao giờ kích hoạt**
  (không có DELETE thật). Ràng buộc "không xóa category còn recipe" hoàn toàn nằm ở
  `DeleteCategoryCommandHandler`: đếm recipe chưa xóa trong category → nếu > 0 thì
  409 `CATEGORY_DELETE_HAS_RECIPES`.
- Đếm recipe phải **bỏ qua recipe đã soft-delete** (Global Query Filter làm sẵn việc này).
- `Categories.Name` và `Categories.Slug` có UNIQUE — tên category đã xóa không dùng lại được.
- **Sửa SRS:** FR-CAT-005 bước 5 — "Xóa entity" → "Đặt IsDeleted = true".

---

## D3 — Publish cần ít nhất 1 step VÀ 1 ingredient

**Chốt:** `recipe.Publish()` ném `DomainException` nếu `Steps.Count == 0` **hoặc**
`Ingredients.Count == 0`.

**Vì:** Phụ lục B (`RECIPE_PUBLISH_INCOMPLETE`) nói rõ cần cả hai. Một công thức nấu ăn
không có nguyên liệu thì vô nghĩa — đây là ràng buộc nghiệp vụ đúng.

**Hệ quả:**

- Thông báo lỗi phải nói rõ thiếu cái nào: "Công thức phải có ít nhất 1 nguyên liệu và
  1 bước thực hiện." Trả 400 `RECIPE_PUBLISH_INCOMPLETE` (xem D4).
- **Sửa SRS:** FR-RCP-005 — bổ sung điều kiện ingredient vào mô tả, tiền đề và bước 5.

---

## D4 — Validation → 400 · Concurrency conflict → 409

**Chốt:**

| Tình huống | HTTP | Application Error Code |
|---|---|---|
| FluentValidation fail | **400** | `VALIDATION_ERROR` |
| Vi phạm business rule (publish thiếu step/ingredient) | **400** | `RECIPE_PUBLISH_INCOMPLETE` |
| RowVersion mismatch | **409** | `RECIPE_CONCURRENCY_CONFLICT` |
| Trùng unique field (category name) | **409** | `CATEGORY_NAME_EXISTS`, `AUTH_EMAIL_EXISTS` |

**422 Unprocessable Entity không còn được dùng ở đâu trong hệ thống.**

**Vì:** Chương 8 và Phụ lục B đều dùng 400 cho validation (2/3 nguồn). Và 409 Conflict là
mã đúng ngữ nghĩa HTTP cho "trạng thái tài nguyên đã đổi" — FR-RCP-004 đúng, Phụ lục A/B sai.

**Hệ quả:**

- `GlobalExceptionMiddleware` map: `ValidationException` → 400 · `DomainException` → 400 ·
  `DbUpdateConcurrencyException` → 409 · `ConflictException` → 409 · `NotFoundException` → 404 ·
  `ForbiddenException` → 403.
- **Sửa SRS:** thay toàn bộ "422" trong Chương 3 bằng 400 hoặc 409 theo bảng trên;
  bỏ dòng 422 khỏi Phụ lục A; sửa `RECIPE_CONCURRENCY_CONFLICT` từ 422 thành 409.

---

## D5 — Trường hồ sơ người dùng

**Chốt:** theo mô hình dữ liệu 7.7 và Chương 8.

| Endpoint | Body / Response |
|---|---|
| `POST /auth/register` | `{ email, password, displayName }` |
| `POST /auth/login` | `{ email, password }` |
| `GET /auth/me` | `{ id, email, displayName, avatarUrl, bio, roles }` |
| `PATCH /auth/me` | `{ displayName?, avatarUrl?, bio? }` |

**Không tồn tại** trường `fullName` và `userName` trong bất kỳ request/response nào.
`UserName` của Identity được set bằng `email` khi đăng ký (Identity yêu cầu có giá trị).

**Vì:** cột `FullName` không tồn tại trong bảng `AspNetUsers`. Code theo Chương 3 thì
migration fail.

**Ràng buộc validator:** `displayName` 2–100 ký tự, NOT NULL · `avatarUrl` URL hợp lệ nếu có ·
`bio` tối đa 1000 ký tự · `email` đúng format · `password` xem phần *Ràng buộc validator*
cuối file.

**Không trả `emailConfirmed`** (xem D12 — không dùng để chặn gì).

**Sửa SRS:** FR-AUTH-001, 003, 006, 007 — thay `fullName`/`userName` bằng `displayName`,
bổ sung `bio`, bỏ `emailConfirmed` khỏi response.

---

## D6 — RecipeStep

**Chốt:**

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|:--:|---|
| `title` | string ≤ 200 | ✅ | Theo mô hình 7.3 (NOT NULL) |
| `description` | string ≤ 2000 | ✅ | Ràng buộc độ dài ở validator |
| `timerMinutes` | int ≥ 0 | ❌ | **Tên chuẩn** — không dùng `durationMinutes` |
| `imageUrl` | string ≤ 500 | ❌ | |
| `stepNumber` | int | — | **Server sinh** ở POST; **client gửi được ở PUT** để đổi vị trí |

**Hành vi `stepNumber`:**

- `POST` — body **không chứa** `stepNumber`. Server đặt `Max(StepNumber) + 1`, hoặc `1`
  nếu chưa có bước nào.
- `PUT` — body có `stepNumber?`. Nếu có, server chèn bước vào vị trí đó và **renumber lại
  toàn bộ** các bước còn lại cho liên tục.
- `DELETE` — sau khi xóa, **renumber lại** để `StepNumber` luôn liên tục 1, 2, 3…

**Vì:** FR-RCP-010 mô tả rõ logic server sinh và auto-renumber — đó là hành vi có chủ đích.
Cho client gửi `stepNumber` tự do ở POST thì phải xử lý trùng, phức tạp hơn mà không được gì.

**Sửa SRS:** FR-RCP-010 — đổi `durationMinutes` → `timerMinutes`, bổ sung `title` vào
request body. Chương 8 mục 8.5 — bỏ `stepNumber` khỏi body của POST.

---

## D7 — RecipeIngredient

**Chốt:** theo mô hình dữ liệu 7.4.

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|:--:|---|
| `name` | string 1–**200** | ✅ | |
| `quantity` | decimal(10,3) | ❌ | **Nullable** — cho "muối vừa đủ", "tiêu tùy khẩu vị" |
| `unit` | string ≤ 50 | ❌ | **Nullable** |
| `notes` | string ≤ 500 | ❌ | |
| `orderIndex` | int | ❌ | **Tên chuẩn** — không dùng `sortOrder`. Mặc định 0 |

Nếu `quantity` có giá trị thì phải `> 0`.

**Vì:** mô hình dữ liệu cố ý cho nullable để hỗ trợ "nguyên liệu vừa đủ" — rất đúng với
công thức nấu ăn Việt. FR-RCP-009 bắt buộc `> 0` là mâu thuẫn với chính ý đồ đó.

**Sửa SRS:** FR-RCP-009 — bỏ "Quantity > 0" và "Unit không rỗng" khỏi tiền đề,
đổi `sortOrder` → `orderIndex`, đổi `Name 1–100` → `1–200`.

---

## D8 — Cache: chỉ Redis, qua MediatR pipeline

**Chốt:** **một cơ chế duy nhất** — Redis (`StackExchange.Redis`) truy cập qua
`CachingBehavior` và `CacheInvalidationBehavior` trong MediatR pipeline.

**Bỏ hoàn toàn:** `IMemoryCache`, ASP.NET Output Cache.

**Vì:** NFR-SCALE-001 cấm thẳng `IMemoryCache` ("distributed cache, **không** in-memory
IMemoryCache, cho mọi shared state"), và Chương 6.3 đã đặc tả `CachingBehavior` đọc Redis.
Dùng Output Cache song song thì thành cache 2 lớp, invalidation phải làm 2 nơi — nguồn bug.

### Cache key và TTL

| Key | TTL | Tag | Nguồn TTL |
|---|---|---|---|
| `categories:all` | **30 phút** | `categories` | NFR-PERF-003 (thắng FR-CAT-001: 60 phút) |
| `recipes:list:{hash(queryString)}` | **15 phút** | `recipes` | FR-RCP-001 (NFR im lặng) |
| `recipe:{slug}` | **5 phút** | `recipes`, `recipe:{slug}` | NFR-PERF-003 (thắng FR-RCP-002: 60 phút) |
| `recipes:search:{hash(q+params)}` | **1 phút** | `recipes` | NFR-PERF-003 (thắng FR-SRCH-001: 5 phút) |

### Quy tắc invalidation

| Command | Xóa tag |
|---|---|
| Create/Update/Delete **Category** | `categories` **và** `recipes` |
| Create/Update/Delete/Publish/Archive **Recipe** | `recipes` và `recipe:{slug}` |
| Thao tác Step / Ingredient / Image | `recipes` và `recipe:{slug}` |

> Tại sao sửa Category lại xóa cả `recipes`: `RecipeSummaryDto` và `RecipeDetailDto` có
> nhúng tên category. Đổi tên category mà không xóa cache recipe thì client thấy tên cũ
> tới 15 phút.

**Cache miss không được làm sập hệ thống.** Redis down → `CachingBehavior` log warning
và đi thẳng xuống handler (NFR-REL-002).

**Sửa SRS:** FR-CAT-001/003 bỏ `IMemoryCache`; FR-RCP-001/002 bỏ Output Cache;
thống nhất TTL theo bảng trên.

---

## D9 — Google OAuth: ID Token

**Chốt:** `POST /api/v1/auth/google` với body `{ idToken }`.

- Frontend dùng Google Sign-In (qua Auth.js v5 Google provider) để lấy ID Token.
- Backend verify ID Token với Google, lấy `email`, `name`, `picture`, `sub` (providerKey).
- **Không có** endpoint `/auth/google/callback`.

**Vì:** khớp với Chương 8 (API spec là nguồn chuẩn cho endpoint). Đơn giản nhất trong
ba phương án và không cần backend giữ client secret trong luồng redirect.

**Xử lý tài khoản trùng email** (giữ nguyên FR-AUTH-003 bước 7–8):

- Chưa có user với email đó → tạo `ApplicationUser` mới, `displayName` = Google `name`,
  `avatarUrl` = Google `picture`, gán role `Author`, gọi `AddLoginAsync`.
- Đã có user đăng ký thủ công với email đó → **`AddLoginAsync` liên kết** vào tài khoản
  hiện có. **Không tạo tài khoản thứ hai.**

**Sửa SRS:** FR-AUTH-003 — thay `ExternalLoginInfo` bằng `idToken`; Chương 5.3 — bỏ
"Authorization Code + PKCE" và redirect URI.

---

## D10 — Slug trùng: tự thêm hậu tố

**Chốt:** `SlugHelper.Generate(text)` sinh slug; nếu trùng thì thêm `-2`, `-3`, `-4`…
cho đến khi unique. Áp dụng cho **cả Recipe và Category**.

**Bỏ error code `RECIPE_SLUG_EXISTS`** — tình huống đó không bao giờ xảy ra.
Giữ `CATEGORY_NAME_EXISTS` (409) vì `Categories.Name` có UNIQUE constraint thật.

**Vì:** mô hình 7.2 nói rõ `Recipe.Title` được phép trùng ("có thể trùng title, khác nhau slug")
— nghĩa là bắt buộc phải auto-suffix. FR-CAT-003 đã làm đúng như vậy cho Category.

**Quy tắc sinh slug:** bỏ dấu tiếng Việt (`unaccent`) → lowercase → thay ký tự không phải
`[a-z0-9]` bằng `-` → gộp `-` liên tiếp → cắt `-` ở đầu/cuối → giới hạn 200 ký tự.
Ví dụ: `"Phở Bò Tái Nạm"` → `pho-bo-tai-nam`.

**Slug cấm:** `search`, `new`, `edit` — tránh đụng route. `SlugHelper` thấy slug rơi vào
danh sách này thì thêm `-1`.

**Sửa SRS:** FR-RCP-003 A3 — bỏ 409, thay bằng auto-suffix. Phụ lục B — bỏ `RECIPE_SLUG_EXISTS`.

---

## D11 — Admin quản lý user: ngoài scope v1.0.0

**Chốt:** **không** có endpoint Admin quản lý tài khoản trong v1.0.0.

**Giữ lại:**

- Cột `AspNetUsers.IsActive` (mặc định `true`).
- Kiểm tra khi đăng nhập: `IsActive == false` → **403** `AUTH_ACCOUNT_DISABLED`.
- Áp dụng cho cả `POST /auth/login` và `POST /auth/refresh`.

**Không làm:** endpoint `PATCH /admin/users/{id}`, route `/dashboard/users`, danh sách user.
Cần ban ai thì Admin `UPDATE AspNetUsers SET "IsActive" = false` trực tiếp trong DB.

**Vì:** đồ án nhỏ, tính năng này được nhắc 3 lần nhưng chưa bao giờ được đặc tả — thêm vào
là mở rộng scope ngoài SRS.

**Sửa SRS:** ghi rõ trong mục 1.2.3 (Những gì KHÔNG thuộc phạm vi): "Quản lý người dùng
bởi Admin (khóa/mở khóa tài khoản qua giao diện)".

---

## D12 — Email verification: ngoài scope v1.0.0

**Chốt:**

- **Bỏ policy `VerifiedAuthor`** khỏi hệ thống. Không đăng ký, không dùng.
- Chỉ còn 2 policy: `AuthorPolicy` và `AdminPolicy`.
- Email chào mừng (FR-JOB-001) **không có link kích hoạt** — chỉ là email chào mừng
  kèm link về trang chủ và link tới `/dashboard`.
- `GET /auth/me` **không trả** `emailConfirmed`.
- Cột `EmailConfirmed` của Identity vẫn tồn tại (không bỏ được) nhưng không dùng để chặn gì.

**Vì:** policy được nhắc ở mục 2.3 nhưng **không endpoint nào trong Chương 8 dùng nó**, và
không có FR nào cho luồng xác nhận email. Nó là tàn dư của bản nháp.

**Sửa SRS:** mục 2.3 — bỏ đoạn "(3) Policy-Based Authorization: Policy VerifiedAuthor…".
FR-JOB-001 — bỏ "link kích hoạt email (nếu cần)".

---

## D13 — Bỏ yêu cầu 301 redirect khi slug đổi

**Chốt:** không hiện thực 301 redirect. Không thêm bảng lưu slug cũ.

**Vì:** NFR-SEO-004 nói slug "không thay đổi sau khi publish", nên redirect chỉ áp dụng cho
recipe Draft. Mà Draft thì `noindex` — không có SEO để mất. **Yêu cầu tự vô hiệu.**

**Quy tắc thay thế:** slug sinh một lần lúc tạo recipe và **không bao giờ đổi**, kể cả khi
Author sửa title. Đơn giản, không mất SEO, không cần bảng lịch sử.

**Sửa SRS:** NFR-SEO-004 — bỏ gạch đầu dòng "Redirect". Ghi rõ "slug bất biến sau khi tạo".

---

## D14 — Filter `minServings`: có hỗ trợ

**Chốt:** `GET /api/v1/recipes` nhận đủ 4 filter, kết hợp bằng AND:

```
?categoryId={guid}&difficulty={Easy|Medium|Hard|Expert}&maxCookTime={phút}&minServings={n}
```

**Vì:** FR-SRCH-002 là FR chuyên trách về filter, nó liệt kê đủ 4. Query string trong
FR-RCP-001 chỉ là ví dụ rút gọn.

**Sửa SRS:** FR-RCP-001 — bổ sung `minServings` vào query string mẫu.

---

## D15 — Rate limit theo NFR-SEC-003

**Chốt:**

| Nhóm endpoint | Giới hạn |
|---|---|
| `/api/v1/auth/*` | 10 req/phút/IP (Fixed Window) |
| API chung | 100 req/phút/IP (Sliding Window) |
| `POST /recipes/{id}/images` | 5 req/phút/IP |

Vượt → **429** `RATE_LIMIT_EXCEEDED` + header `Retry-After`.
Response luôn kèm `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.

**Vì:** NFR-SEC-003 là nguồn chuyên trách. Con số `100` trong ví dụ ở Chương 5.2 là minh họa
cho API chung, không phải cho `/auth/*`.

**Sửa SRS:** Chương 5.2 — ghi chú rõ ví dụ header là của API chung.

---

## D16 — Bucket MinIO giữ `public-read`

**Chốt:** bucket `culinary-blog` giữ policy `public-read`. **Chấp nhận** việc ảnh của recipe
Draft truy cập được bằng URL trực tiếp.

**Vì:** URL chứa GUID v4 (`recipes/{recipeId}/{guid}.jpg`) — không đoán được trong thực tế.
Chuyển sang presigned URL làm phức tạp frontend (URL hết hạn, phải refresh) mà lợi ích bảo mật
rất nhỏ với một blog ẩm thực công khai.

**Rủi ro chấp nhận:** nếu ai đó có được URL ảnh của recipe Draft (ví dụ Author tự share nhầm),
họ xem được ảnh đó dù chưa publish. Không lộ dữ liệu văn bản của recipe.

**Ghi vào:** `docs/adr/0001-minio-public-read.md`.

---

## D17 — Tài khoản bị khóa: 423 + `AUTH_ACCOUNT_LOCKED`

**Chốt:** sai mật khẩu 5 lần → Identity lockout 15 phút → **423 Locked** với
error code `AUTH_ACCOUNT_LOCKED`, `detail` ghi số phút còn lại.

**Bổ sung vào SRS:**

- Phụ lục A: `423 Locked — Tài khoản bị khóa tạm thời do đăng nhập sai quá số lần cho phép.`
- Phụ lục B: `AUTH_ACCOUNT_LOCKED | 423 | Tài khoản bị khóa tạm thời (15 phút) sau 5 lần
  đăng nhập sai. | Auth`

**Cấu hình `IdentityOptions.Lockout`:** `MaxFailedAccessAttempts = 5`,
`DefaultLockoutTimeSpan = 15 phút`, `AllowedForNewUsers = true`.

**Vì:** FR-AUTH-002 A2 đã quy định 423, chỉ là Phụ lục A/B quên liệt kê. Giữ 423 đúng hơn
429 vì đây là trạng thái tài khoản, không phải giới hạn tốc độ.

---

## D18 — `Recipe.Instructions` đổi sang NULL

**Chốt:** cột `Instructions` trong bảng `Recipes` là **`text NULL`**.

**Vì:** chính SRS gọi nó là "legacy field" và nói chi tiết dùng `RecipeSteps`. Request body
của FR-RCP-003 và Chương 8 đều cho `instructions?` optional. Cột NOT NULL mà body optional
thì phải nhét chuỗi rỗng — vô nghĩa.

**Sửa SRS:** mô hình 7.2 — `Instructions text NOT NULL` → `text NULL`.

---

## D19 — `CookTime` cho phép 0

**Chốt:** validator dùng:

```
prepTime  > 0       (phút chuẩn bị, luôn có)
cookTime  >= 0      (0 = món không cần nấu: salad, gỏi, sinh tố)
servings  > 0
```

**Vì:** mô hình 7.2 ghi `CHECK >= 0` kèm chú thích "0 cho *No cook* recipes". FR-RCP-003
bước 4 viết `> 0` cho cả ba, sẽ chặn oan món nguội.

**Sửa SRS:** FR-RCP-003 bước 4 — tách riêng điều kiện cho `cookTime`.

---

## D20 — RefreshToken theo schema 7.8

**Chốt:** bảng **`RefreshTokens`** (PascalCase, không phải `refresh_tokens`) với đúng các cột
trong mô hình 7.8:

```
Id, UserId, TokenHash (SHA-256, UNIQUE), ExpiresAt, RevokedAt,
ReplacedByTokenHash, CreatedAt, CreatedByIp
```

- **Không có cột `IsRevoked`.** Trong C# nó là computed property:
  `public bool IsRevoked => RevokedAt != null;`
- **Không lưu raw token.** Chỉ lưu SHA-256 hash. Khi client gửi refresh token lên,
  server hash rồi tra theo `TokenHash`.
- **Không có cột `ReplacedByToken`** — chỉ `ReplacedByTokenHash`.
- Token còn hiệu lực khi: `RevokedAt == null && ExpiresAt > UtcNow`.

**Reuse detection:** nhận refresh token có `RevokedAt != null` → revoke **toàn bộ token
family** của user đó (truy ngược theo `ReplacedByTokenHash`), log `WARNING`, trả 401
`AUTH_REFRESH_TOKEN_REVOKED`.

> **Đã được D35-5 thay phạm vi revoke:** không truy chuỗi `ReplacedByTokenHash` nữa mà revoke
> **mọi RT còn hiệu lực của user**. Phần còn lại của D20 (schema, hash, điều kiện còn hiệu lực)
> giữ nguyên.

**Vì:** schema 7.8 đúng hơn về bảo mật. Chương 3 viết theo bản nháp cũ.

**Sửa SRS:** FR-AUTH-001 bước 10 và FR-AUTH-004 bước 4–5 — dùng đúng tên bảng và tên cột.

---

## D21 — Tổng số FR là 34

**Chốt:** hệ thống có **34 Functional Requirements**:
AUTH 7 + CAT 5 + RCP 10 + SRCH 4 + FILE 2 + JOB 3 + OBS 3 = 34.

**Sửa SRS:** Chương 1.5 và Chương 3 — đổi "27" thành "34".

---

## D22 — Sửa metadata ảnh qua một endpoint PATCH chung

**Chốt:**

| Method | Endpoint | Body |
|---|---|---|
| `POST` | `/api/v1/recipes/{id}/images` | multipart: `file` (bắt buộc), `altText?` |
| `PATCH` | `/api/v1/recipes/{id}/images/{imageId}` | `{ altText?, isPrimary?, orderIndex? }` |
| `DELETE` | `/api/v1/recipes/{id}/images/{imageId}` | — |

**Bỏ** endpoint `PATCH /recipes/{id}/images/{imageId}/primary`.

**Logic `isPrimary`:**

- Upload ảnh **đầu tiên** của recipe → server tự đặt `IsPrimary = true`. Client không gửi.
- `PATCH` với `isPrimary: true` → đặt ảnh này primary, **tất cả ảnh khác của recipe về `false`**.
- `PATCH` với `isPrimary: false` trên ảnh đang primary → **từ chối 400** (recipe phải luôn có
  đúng 1 ảnh primary nếu có ảnh).
- `DELETE` ảnh đang primary → ảnh còn lại có `orderIndex` nhỏ nhất tự lên primary.

**Vì:** Chương 8 là nguồn chuẩn cho endpoint, và một PATCH metadata chung RESTful hơn
ba endpoint con.

**Sửa SRS:** FR-RCP-008 — gộp bước 9–11 vào endpoint PATCH chung.

---

## Ràng buộc validator (gộp các điểm nhỏ)

| Đối tượng | Ràng buộc chốt |
|---|---|
| `password` | ≥ 8 ký tự, có ≥ 1 chữ hoa, ≥ 1 chữ **thường**, ≥ 1 số, ≥ 1 ký tự đặc biệt (theo NFR-SEC-001 — FR-AUTH-001 thiếu "chữ thường") |
| `Recipe.Title` | 5–200 ký tự |
| `Recipe.Description` | 1–2000 ký tự (mô hình 7.2 không có CHECK — ràng buộc ở validator) |
| `RecipeStep.Description` | 1–2000 ký tự (mô hình 7.3 là `text` — ràng buộc ở validator) |
| `Category.Name` | 2–50 ký tự, không chứa HTML |
| `Category.Description` | Tùy chọn, ≤ 500 ký tự, không chứa HTML (mô hình 7.x là `text` — ràng buộc ở validator, áp dụng cho cả FR-CAT-003 và FR-CAT-004) |
| `q` (search) | ≥ 2 ký tự |
| `pageSize` | 1–50, mặc định 12. Vượt 50 → **clamp về 50**, không trả lỗi |
| `page` | ≥ 1, mặc định 1 |

**Thứ tự đăng ký route:** `/recipes/search` phải đăng ký **trước** `/recipes/{slug}`,
nếu không request `/recipes/search` sẽ khớp vào route slug. Kèm theo D10 cấm slug `search`.

---

## Danh sách Application Error Code sau khi chốt

| Error Code | HTTP | Module | Thay đổi |
|---|---|---|---|
| `AUTH_EMAIL_EXISTS` | 409 | Auth | — |
| `AUTH_INVALID_CREDENTIALS` | 401 | Auth | — |
| `AUTH_TOKEN_EXPIRED` | 401 | Auth | — |
| `AUTH_TOKEN_INVALID` | 401 | Auth | — |
| `USER_NOT_FOUND` | 404 | Auth | D48 |
| `AUTH_REFRESH_TOKEN_EXPIRED` | 401 | Auth | — |
| `AUTH_REFRESH_TOKEN_REVOKED` | 401 | Auth | — |
| `AUTH_GOOGLE_TOKEN_INVALID` | 400 | Auth | — |
| `AUTH_GOOGLE_UNAVAILABLE` | **502** | Auth | 🆕 **thêm mới** (D33) — Google API không gọi được (SRS A3) |
| `AUTH_ACCOUNT_DISABLED` | 403 | Auth | — |
| `AUTH_ACCOUNT_LOCKED` | **423** | Auth | 🆕 **thêm mới** (D17) |
| `RECIPE_NOT_FOUND` | 404 | Recipe | — |
| ~~`RECIPE_SLUG_EXISTS`~~ | — | — | ❌ **bỏ** (D10) |
| `RECIPE_PUBLISH_INCOMPLETE` | 400 | Recipe | — |
| `RECIPE_FORBIDDEN` | 403 | Recipe | — |
| `RECIPE_CONCURRENCY_CONFLICT` | **409** | Recipe | ✏️ đổi từ 422 (D4) |
| `RECIPE_IMAGE_NOT_FOUND` | 404 | Recipe | 🆕 **thêm mới** — SRS FR-RCP-008 chỉ ghi chung "404", chưa có mã riêng cho ảnh không tồn tại (phát hiện lúc lập kế hoạch FR-RCP-008) |
| `RECIPE_PRIMARY_IMAGE_REQUIRED` | 400 | Recipe | 🆕 **thêm mới** (D22) — `PATCH` `{ isPrimary: false }` trên ảnh đang là primary bị từ chối, vì recipe phải luôn có đúng 1 ảnh primary nếu còn ảnh |
| `RECIPE_STEP_NOT_FOUND` | 404 | Recipe | 🆕 **thêm mới** (D73) — bước không tồn tại hoặc thuộc recipe khác |
| `RECIPE_INGREDIENT_NOT_FOUND` | 404 | Recipe | 🆕 **thêm mới** (D76) — nguyên liệu không tồn tại hoặc thuộc recipe khác |
| `CATEGORY_NOT_FOUND` | 404 | Category | — |
| `CATEGORY_NAME_EXISTS` | 409 | Category | — |
| `CATEGORY_DELETE_HAS_RECIPES` | 409 | Category | — |
| `CATEGORY_FORBIDDEN` | 403 | Category | 🆕 **thêm mới** (D50) — người không phải Admin gọi tạo/sửa/xóa category |
| `FILE_SIZE_EXCEEDED` | 400 / **413** | File | 413 khi body vượt giới hạn tầng HTTP (D67) |
| `FILE_MIME_INVALID` | 400 | File | — |
| `VALIDATION_ERROR` | 400 | Common | — |
| `RATE_LIMIT_EXCEEDED` | 429 | Common | — |

**HTTP status còn dùng:** 200, 201, 204, 400, 401, 403, 404, 409, 423, 429, 500, 503.
**Không còn dùng 422.**

---

## D23 — `ApplicationUser` thuộc tầng Infrastructure, không phải Domain

**Nguồn mâu thuẫn:** SRS mục 1008 (bảng kiến trúc) liệt `ApplicationUser` vào danh sách
Domain Entities, và `docs/roadmap.md`/`Domain/Entities/OWNER.md` ghi `ApplicationUser.cs`
là file cần tạo trong `CulinaryBlog.Domain/Entities/`, kế thừa `IdentityUser<string>`.
Nhưng CONS-001 (bất biến, không thương lượng): **"Domain KHÔNG phụ thuộc thư viện ngoài
nào (chỉ .NET BCL)"** — và `Domain.csproj` có comment tường minh "KHÔNG thêm
`<PackageReference>` vào file này". `IdentityUser<TKey>` nằm trong gói NuGet
`Microsoft.Extensions.Identity.Stores`, và để dùng được `AddEntityFrameworkStores<TContext>()`
(cần cho FR-AUTH-001 → 007), `ApplicationUser` **bắt buộc** phải kế thừa `IdentityUser<TKey>`.
Hai yêu cầu này không thể cùng đúng.

**Chốt:** `ApplicationUser` đặt tại `CulinaryBlog.Infrastructure/Identity/ApplicationUser.cs`,
kế thừa `IdentityUser` (khóa `string`, theo đúng SRS 7.7 "Id varchar(450)"). **Domain không
có entity User.** Application tầng trên không được biết kiểu `ApplicationUser` — mọi thao
tác Identity (tạo user, gán role, kiểm tra password, tìm theo email/id) đi qua interface
`IIdentityService` khai báo ở `Application/Common/Interfaces/IIdentityService.cs`, hiện thực
ở `Infrastructure/Identity/IdentityService.cs` bằng `UserManager<ApplicationUser>`.

`RefreshToken` (SRS 7.8) **vẫn ở Domain** — nó là POCO thuần, không kế thừa `IdentityUser`,
chỉ giữ `UserId` kiểu `string` (không có navigation property đến `ApplicationUser`, vì kiểu
đó không tồn tại ở Domain). Quan hệ FK `RefreshTokens.UserId → AspNetUsers.Id` được cấu hình
ở Infrastructure (Fluent API), không qua navigation property.

**Vì:** CONS-001 nằm trong danh sách 10 ràng buộc "vi phạm = reject PR" — không thể xếp
ngang hàng với một dòng liệt kê trong bảng kiến trúc tổng quan (SRS 1008) hay một ghi chú
điều phối công việc (roadmap.md, không phải nguồn sự thật theo mục 1 CLAUDE.md). Mẫu hình
"contract ở Application, implementation ở Infrastructure" cũng chính là mẫu hình dự án đã
dùng sẵn cho `IJwtService`, `IEmailService`, `ICurrentUser`, `IFileStorageService` — áp dụng
tiếp cho Identity là nhất quán, không phải kiến trúc mới.

**Đây là quyết định kiến trúc** (không chỉ làm rõ SRS) → xem thêm `docs/adr/0003-application-user-o-tang-infrastructure.md`.

**Hệ quả:**

- `CulinaryBlogDbContext` đổi lớp cha thành
  `IdentityDbContext<ApplicationUser, IdentityRole, string>`.
- `IRefreshTokenRepository` (Domain/Interfaces) — vì `RefreshToken` không kế thừa
  `BaseEntity` (không có `IsDeleted`/`RowVersion`, xem D20) nên không dùng được
  `IRepository<T>` chung.
- Domain vẫn **0 package reference** — ArchitectureTests `Domain_ShouldNotDependOn_EntityFramework`
  và các test dependency-rule khác tiếp tục pass nguyên trạng.

**Sửa SRS:** mục 1008 (bảng kiến trúc) — bỏ `ApplicationUser` khỏi danh sách "Entities" của
Domain Layer, ghi chú đây là entity của Infrastructure truy cập qua `IIdentityService`.

---

## D24 — Response của `POST /auth/register`: đầy đủ `AuthResponseDto`, không phải `{userId,email,displayName}`

**Nguồn mâu thuẫn:** SRS Chương 3 (FR-AUTH-001, mô tả + luồng chính bước 7–12) nói **hai lần**
rằng đăng ký xong thì "tự động được gán role Author và **nhận bộ token để truy cập ngay lập
tức (auto-login sau đăng ký)**", rồi liệt chi tiết: tạo access token, tạo refresh token, lưu
refresh token, trả về **`AuthResponseDto`** đầy đủ. Nhưng bảng tóm tắt endpoint ở Chương 8
mục 8.1, dòng `POST /auth/register`, ghi response 201 chỉ là `{ userId, email, displayName }`
— không có token nào.

**Chốt:** Response `201 Created` của `POST /api/v1/auth/register` là **`AuthResponseDto`
đầy đủ**, giống hệt response của `/auth/login` (FR-AUTH-002):

```
{ accessToken, refreshToken, expiresAt, user: { id, email, displayName, avatarUrl, bio, roles } }
```

(trường `user` theo đúng shape D5 của `GET /auth/me`, trừ khi `bio` mới tạo sẽ là `null`.)

**Vì:** mô tả nghiệp vụ ở Chương 3 nhất quán nội bộ, lặp lại rõ ràng ý định "auto-login", và
mô tả chi tiết từng bước sinh access token + refresh token + lưu DB — đây rõ ràng là hành vi
có chủ đích, không phải lỗi đánh máy. Bảng Chương 8 mục 8.1 là bảng tóm tắt: nhiều dòng khác
trong cùng bảng đó cũng lược bớt trường (ví dụ không dòng nào trong 8.1 liệt `roles` dù chắc
chắn cần cho phân quyền FE) — nhiều khả năng người viết bảng tóm tắt chỉ ghi vài field đại
diện. Vì vậy ở đây Chương 3 (mô tả luồng chi tiết) thắng Chương 8 (bảng tóm tắt) cho riêng
nội dung response, dù nhìn chung Chương 8 vẫn là nguồn chuẩn cho method/path/endpoint shape
(D9, D22 đã dùng lý lẽ ngược lại đúng chỗ của nó — ở đó chính luồng chi tiết Chương 3 mới là
bản nháp cũ).

**Sửa SRS:** Chương 8 mục 8.1, dòng `POST /auth/register` — sửa response 201 thành đầy đủ
`AuthResponseDto` như trên.

---

## D25 — Độ dài refresh token: 128-bit, không phải 512-bit

**Nguồn mâu thuẫn:** FR-AUTH-001 bước 9 (Chương 3) ghi "JwtService.GenerateRefreshToken() –
tạo refresh token ngẫu nhiên (**512-bit**, 7 ngày)". NFR-SEC-002 (mục bảo mật) ghi "Refresh
Token: **128-bit** cryptographically secure random bytes... TTL = 7 ngày".

**Chốt:** **128-bit** (16 byte) random, sinh bằng `RandomNumberGenerator`, encode Base64
cho client, hash SHA-256 (32 byte / 64 hex char, khớp `TokenHash varchar(64)` ở D20/SRS 7.8)
trước khi lưu DB.

**Vì:** NFR-SEC-002 là đặc tả bảo mật riêng, có chủ đích (nêu rõ thuật toán + TTL + cơ chế
rotation trong cùng một đoạn nhất quán nội bộ) — đáng tin hơn một con số nhắc thoáng qua
trong mô tả luồng nghiệp vụ ở Chương 3. `docs/roadmap.md` mục S2 (tài liệu điều phối công
việc, viết sau khi đã đọc kỹ cả hai nguồn) cũng đã chốt sẵn "128-bit" — nhất quán với lựa
chọn này.

**Sửa SRS:** Chương 3, FR-AUTH-001 bước 9 — đổi "512-bit" thành "128-bit" cho khớp NFR-SEC-002.

---

## D26 — `RefreshToken` đặt ở Domain, không phải Infrastructure

**Nguồn mâu thuẫn:** đây không phải mâu thuẫn trong SRS mà là va chạm giữa hai nhánh code
độc lập. Sprint 0 (đã merge vào `main`) tự tạo `RefreshToken` tại
`Infrastructure/Identity/RefreshToken.cs` cùng lúc PR FR-AUTH-001 (nhánh
`2312800_VCVinh_FR-AUTH-001`, code trước khi rebase) tạo một bản khác gần như y hệt tại
`Domain/Entities/RefreshToken.cs` — hai bên không biết nhau vì nhánh PR rẽ ra từ trước khi
Sprint 0 được merge. Merge hai nhánh tạo `add/add conflict` thật trên
`ApplicationUser.cs`, `RefreshTokenConfiguration.cs`, `CulinaryBlogDbContextModelSnapshot.cs`.

**Chốt:** giữ `RefreshToken` ở **`Domain/Entities/RefreshToken.cs`** (theo bản PR FR-AUTH-001),
bỏ bản ở `Infrastructure/Identity/RefreshToken.cs` (bản Sprint 0).

**Vì:** `RefreshToken` là POCO thuần, không kế thừa kiểu gì từ NuGet package nào — đặt ở
Domain không vi phạm CONS-001. Đặt ở Domain còn cho phép `RegisterCommandHandler` (và các
handler FR-AUTH-002/004 sau này) ở tầng Application **tạo `RefreshToken` trực tiếp** qua
`RefreshToken.Create(...)`, đúng CONS-001 (Application → Domain, không → Infrastructure).
Nếu để `RefreshToken` ở Infrastructure như Sprint 0 làm, Application sẽ phải thêm một lớp
gián tiếp kiểu `IIdentityService` chỉ để tạo một POCO không có logic phụ thuộc gì — không
cần thiết. `ApplicationUser` là trường hợp khác: nó **bắt buộc** kế thừa `IdentityUser<TKey>`
(gói `Microsoft.Extensions.Identity.Stores`) nên không có lựa chọn nào khác ngoài Infrastructure
(xem D23/ADR-0003) — hai entity không cùng ràng buộc nên không cần cùng vị trí.

**Hệ quả:**

- `RefreshTokenConfiguration.cs` tham chiếu `CulinaryBlog.Domain.Entities.RefreshToken`.
- Không cần migration mới: shape cột trong `RefreshTokens` table giữa hai bản giống hệt
  nhau (namespace không ảnh hưởng schema DB) — migration `InitialCreate` (Sprint 0) vẫn đúng.
- `IRefreshTokenRepository` ở `Domain/Interfaces` (không phải `Application/Common/Interfaces`)
  vì `RefreshToken` không kế thừa `BaseEntity` nên không dùng chung `IRepository<T>` (D20).

**Bài học quy trình:** trước khi mở PR cho một FR đã có người khác chạm vào scaffold
(Sprint 0 tạo entity cho toàn bộ Chương 7, kể cả `ApplicationUser`/`RefreshToken`), phải
`git fetch && git rebase origin/main` trước khi bắt đầu code, không chỉ trước khi mở PR.

---

## D27 — Hợp đồng dữ liệu ảnh công thức (FR-RCP-008)

> Phát hiện lúc lập kế hoạch FR-RCP-008 (2026-09-20). Bổ sung D22, không thay thế.

**Chốt:**

| Vấn đề | Quyết định |
|---|---|
| Response `POST /images` | **201** `{ imageId, originalUrl, altText, isPrimary }` (theo mục 8.4) |
| Body `POST /images` | multipart `file` (bắt buộc) + `altText?`. **Không nhận `isPrimary`** — server tự quyết (D22) |
| `orderIndex` khi upload | `Max(orderIndex của các ảnh hiện có) + 1`; ảnh đầu tiên = `0` |
| `PATCH` không có field nào | **400** `VALIDATION_ERROR` |
| `PATCH.orderIndex` | Phải `>= 0`. **Được phép trùng** với ảnh khác (chỉ là thứ tự hiển thị, không renumber) |
| `PATCH.altText` | Tối đa 200 ký tự (theo 7.5) |
| Recipe ở status nào được sửa ảnh | **Mọi status** (Draft / Published / Archived) — quyền chỉ phụ thuộc ownership |
| Bảo đảm "chỉ 1 ảnh primary" | Domain (`Recipe`) **và** partial unique index `(RecipeId) WHERE "IsPrimary"` ở DB |

**Vì:**

- SRS mâu thuẫn: FR-RCP-008 ghi response `{ url, isPrimary }`, mục 8.4 ghi
  `{ imageId, originalUrl, altText, isPrimary }`. Theo D22, Chương 8 là nguồn chuẩn cho endpoint;
  FE cần `imageId` để gọi PATCH/DELETE nên bản 8.4 mới dùng được.
- SRS 8.4 cho client gửi `isPrimary?` khi upload, trái D22 ("client không gửi").
- `orderIndex`, PATCH rỗng, status của recipe: SRS không nói. Chọn hành vi đơn giản nhất,
  không thêm ràng buộc SRS không yêu cầu.
- Partial unique index hiện thực dòng "Chỉ có 1 ảnh IsPrimary=true / Recipe" của mục 7.5 ở tầng DB.

**Sửa SRS:** FR-RCP-008 — response 201 theo mục 8.4; bỏ `isPrimary` khỏi body POST (mục 8.4);
mục 7.5 bổ sung partial unique index.

---

## D28 — Magic bytes cho upload ảnh (CONS-007, NFR-SEC-004)

> Phát hiện lúc lập kế hoạch FR-RCP-008 (2026-09-20).

**Chốt:** SRS chỉ liệt kê signature của JPEG và PNG, trong khi CONS-007 cho phép 4 định dạng.
Signature đầy đủ:

| MIME | Signature | Đuôi file sinh ra |
|---|---|---|
| `image/jpeg` | `FF D8 FF` | `.jpg` |
| `image/png` | `89 50 4E 47 0D 0A 1A 0A` | `.png` |
| `image/webp` | `52 49 46 46` (`RIFF`) · 4 byte size · `57 45 42 50` (`WEBP`) | `.webp` |
| `image/avif` | 4 byte size · `66 74 79 70` (`ftyp`) · brand `avif` hoặc `avis` (offset 8) | `.avif` |

- Kiểm tra **cả hai**: signature phải khớp một trong bốn định dạng, **và** khớp với `Content-Type`
  client khai báo. Lệch → 400 `FILE_MIME_INVALID`.
- **Đuôi file lấy từ định dạng phát hiện được**, không lấy từ tên file client gửi
  (chống path traversal, NFR-SEC-004). Tên object: `recipes/{recipeId}/{Guid}{ext}`.

**Vì:** không bổ sung thì WebP/AVIF hoặc bị chặn oan, hoặc lọt qua không được kiểm tra.
Chỉ kiểm 4 byte đầu (như SRS bước 4) cũng không phân biệt được WebP với các file RIFF khác (WAV, AVI).

**Sửa SRS:** FR-RCP-008 bước 4 và FR-FILE-001 — bổ sung signature của WebP, AVIF.

---

## D29 — Bug: role hệ thống chưa từng được seed → register/login trả 500

> Phát hiện qua CI thật (2026-09-22), không phải qua đọc SRS — ghi lại vì đây là lỗ hổng
> tồn tại từ Sprint 0, không chỉ riêng PR FR-AUTH-001.

**Hiện tượng:** `POST /auth/register` (và mọi luồng gọi `AddToRoleAsync`) trả 500 khi chạy
với Postgres thật. Không FR nào trong SRS mô tả sai — đây là thiếu sót hiện thực.

**Nguyên nhân:** `.AddRoles<IdentityRole>()` trong `DependencyInjection.cs` chỉ đăng ký
`RoleManager<IdentityRole>`, không tự tạo role nào. Chưa nơi nào trong code (Sprint 0,
`DbSeeder`, hay PR FR-AUTH-001) từng gọi `RoleManager.CreateAsync` để tạo row `"Author"`/
`"Admin"` trong `AspNetRoles`. `UserManager.AddToRoleAsync` khi role không tồn tại **ném
thẳng `InvalidOperationException`** (không phải `IdentityResult.Failed`), không khớp exception
nào của `GlobalExceptionMiddleware` nên rơi vào nhánh 500 mặc định.

**Chốt:** seed 2 role hệ thống qua `IdentityRoleSeeder.SeedAsync(RoleManager<IdentityRole>)`
(`Infrastructure/Identity/IdentityRoleSeeder.cs`), gọi **sau** `Database.MigrateAsync()`:

- `Program.cs`, trong nhánh `IsDevelopment()`.
- `PostgresApiFactory` (integration test), sau migration riêng của factory — vì host được
  build/start trước khi `IsDevelopment()`-block của `Program.cs` kịp chạy trong môi trường
  `"Testing"`.

**Vì:** role là dữ liệu hệ thống bắt buộc để app chạy đúng (khác dữ liệu mẫu của `DbSeeder`),
không phải lựa chọn kiến trúc — không cần ADR, chỉ cần ghi lại để không ai vô tình xóa
`IdentityRoleSeeder` mà không hiểu tại sao nó tồn tại.

**Còn thiếu:** môi trường Production hiện không chạy migration/seed tự động (chỉ
`IsDevelopment()`) — seed role cho Production cần một cơ chế riêng (job/CLI khi deploy),
chưa có trong scope hiện tại. Cần Sprint sau xử lý trước khi go-live.

---

## D30 — GlobalExceptionMiddleware phải giữ ErrorCode riêng của FluentValidation.ValidationException

> Phát hiện lúc lập kế hoạch FR-RCP-008 (2026-09-22) — không phải mâu thuẫn SRS, mà là code
> có sẵn (`GlobalExceptionMiddleware.cs`) không khớp với bảng Application Error Code đã chốt.

**Hiện tượng:** `GlobalExceptionMiddleware` map **mọi** `FluentValidation.ValidationException` về
`type: VALIDATION_ERROR`, bất kể `ValidationFailure.ErrorCode` là gì. Nhưng bảng Application
Error Code (cuối file này) đã chốt `FILE_SIZE_EXCEEDED` và `FILE_MIME_INVALID` là hai mã
**riêng**, khác `VALIDATION_ERROR` — nếu không sửa middleware, response thật của
FR-RCP-008 (upload ảnh sai/quá size) luôn trả sai `type`.

**Chốt:** nếu **mọi** `ValidationFailure` trong exception có cùng một `ErrorCode`, **và** mã đó
đúng định dạng `SCREAMING_SNAKE_CASE` (quy ước Application Error Code — mục 6 CLAUDE.md) → dùng
mã đó làm `type`. Ngược lại (nhiều mã khác nhau, hoặc mã không đúng định dạng) → fallback
`VALIDATION_ERROR` như cũ.

**Vì:** `ValidationFailure.ErrorCode` là cơ chế FluentValidation có sẵn để gắn Application Error
Code riêng cho từng rule (`.WithErrorCode(...)` hoặc gán trực tiếp) — `UploadRecipeImageCommandValidator`
đã dùng nó cho `FILE_SIZE_EXCEEDED`/`FILE_MIME_INVALID`. **Lưu ý quan trọng phát hiện lúc code:**
FluentValidation tự gán `ErrorCode` mặc định = **tên class validator** (vd `"NotEmptyValidator"`,
`"PredicateValidator"`) cho mọi rule KHÔNG tự set — nghĩa là "khác rỗng" không đủ để nhận biết
một Application Error Code thật, bắt buộc phải lọc theo định dạng SCREAMING_SNAKE_CASE, nếu
không middleware sẽ rò rỉ tên validator nội bộ ra `type` cho toàn bộ `ValidationException` có
đúng 1 lỗi trong cả hệ thống (vỡ `RegisterTests`/`LoginTests` hiện có). Trường hợp lỗi hỗn hợp
(nhiều field, nhiều mã) vẫn trả `VALIDATION_ERROR` generic vì frontend không thể hành động theo
một mã duy nhất khi có nhiều loại lỗi khác nhau trong cùng response.

**Sửa code:** `CulinaryBlog.API/Middleware/GlobalExceptionMiddleware.cs`, case
`FluentValidationException` — xem code, không cần sửa SRS (đây không phải mâu thuẫn SRS).

---

## D31 — Bug: thêm child entity vào aggregate đã track bị EF Core hiểu nhầm thành UPDATE

> Phát hiện qua chạy integration test thật lúc code FR-RCP-008 (2026-09-22). **Ảnh hưởng cả
> FR-RCP-009 (ingredient) và FR-RCP-010 (step) — C sẽ gặp đúng lỗi này nếu theo pattern
> tương tự, đọc trước khi code Add/Update ingredient/step.**

**Hiện tượng:** `POST /recipes/{id}/images` (upload ảnh cho recipe đã có sẵn) trả 409
`RECIPE_CONCURRENCY_CONFLICT` dù không ai sửa recipe đồng thời. Log SQL cho thấy EF Core sinh
`UPDATE "RecipeImages" SET ... WHERE "Id" = @p AND "RowVersion" = @p` — tức EF nghĩ ảnh mới là
một dòng **đã tồn tại** cần UPDATE, trong khi nó chưa từng có trong DB, nên `RowVersion` không
khớp → "affected 0 row(s)".

**Nguyên nhân:** Pattern `var image = recipe.AttachImage(...)` chỉ thêm entity mới vào List
backing field của navigation collection (`Recipe.Images`) — KHÔNG gọi `DbSet.Add()`/`AddAsync()`
tường minh. Khi `Recipe` cha đã được EF track (`Unchanged`, load qua
`GetByIdWithImagesAsync`), `ChangeTracker.DetectChanges()` tự phát hiện entity mới này qua
navigation fixup, và phải TỰ ĐOÁN entity đó là `Added` hay `Modified/Unchanged`. Vì
`RecipeImage.Id` (Guid) được gán giá trị thật ngay trong constructor
(`Guid.CreateVersion7()`, xem `BaseEntity`) — KHÔNG phải giá trị CLR default — EF Core coi
"key đã có giá trị" là dấu hiệu "đây là entity đã tồn tại trong DB", nên đoán nhầm thành
`Modified` thay vì `Added`. Heuristic đoán này **chỉ áp dụng khi entity được phát hiện qua
navigation fixup**, không áp dụng khi gọi `Add()`/`AddAsync()` tường minh — gọi tường minh
luôn thắng, bất kể giá trị key.

**Chốt:** mọi Command thêm child entity vào một aggregate root **đã được track từ trước**
(load qua repository rồi mutate, khác với tạo mới toàn bộ graph rồi `AddRange()` một lần như
`DbSeeder`) PHẢI gọi `IRepository<TChild>.AddAsync(child)` tường minh ngay sau khi domain
method trả về entity mới, TRƯỚC `SaveChangesAsync()`. Không được chỉ dựa vào navigation fixup.

```csharp
var image = recipe.AttachImage(url, altText);   // domain: chỉ thêm vào _images
await imageRepository.AddAsync(image, ct);      // BẮT BUỘC — nếu thiếu dòng này sẽ lỗi 409 giả
await unitOfWork.SaveChangesAsync(ct);
```

**Vì:** đây là hành vi mặc định của EF Core với Guid key tự sinh client-side (không phải lỗi
cấu hình RowVersion — xem ghi chú trong `CulinaryBlogDbContext.ApplyRowVersionConcurrencyToken`,
vẫn đúng như thiết kế). Sửa ở Application layer (nơi gọi `AddAsync`) đơn giản và cục bộ hơn
nhiều so với đổi cách sinh Id hoặc cấu hình `ValueGeneratedNever()` cho toàn bộ `BaseEntity`
(ảnh hưởng mọi entity, rủi ro cao hơn lợi ích).

**Cảnh báo cho C (S6/S7 — FR-RCP-009/010):** `Recipe.AddIngredient()`/`AddStep()` hiện tại
(Sprint 0) chỉ dùng cho seeding — nơi cả `Recipe` LẪN children đều mới toanh rồi `AddRange()`
một lần nên không dính bug này. Khi hiện thực `AddIngredientCommand`/`AddStepCommand` thật
(load recipe đã tồn tại qua repository rồi mới thêm ingredient/step), PHẢI áp dụng đúng pattern
`AddAsync()` tường minh ở trên, nếu không sẽ gặp lại đúng lỗi 409 giả này.

---

## D32 — Bug: `CategoryRepository.GetBySlugAsync` thiếu `.Include(Recipes)` → FR-CAT-002 không hoạt động

> Phát hiện lúc review PR #10 (2026-09-23), trước khi merge — không phải mâu thuẫn SRS.

**Hiện tượng:** `GET /api/v1/categories/{slug}` luôn trả `recipes.items = []`, kể cả khi danh
mục có công thức Published thật. Không lỗi biên dịch, không lỗi khi chạy — chỉ sai dữ liệu.

**Nguyên nhân:** `CategoryRepository.GetBySlugAsync` gọi `.AsNoTracking().FirstOrDefaultAsync(...)`
không kèm `.Include(c => c.Recipes)`. Dự án không bật EF Core lazy-loading proxy, nên
`category.Recipes` sau khi query giữ nguyên giá trị khởi tạo field (`= []`) — rỗng vĩnh viễn,
không phải lỗi tạm thời hay N+1 query, mà là dữ liệu sai hoàn toàn im lặng.

**Chốt:** thêm `.Include(c => c.Recipes).ThenInclude(r => r.Images)` vào `GetBySlugAsync`
(cần `Images` để tính `FeaturedImageUrl`). Đây đúng là quy tắc CLAUDE.md mục 6 đã nói rõ:
*"Mọi query EF Core phải có `.Include()`/`.ThenInclude()` hoặc projection — cấm N+1"* — PR chỉ
đơn giản là quên áp dụng, dù method `GetAllWithRecipesAsync` trong cùng file làm đúng.

**Vì sao CI không bắt được:** PR #10 không có integration test nào cho FR-CAT-002. Bug loại
này (sai dữ liệu, không sai HTTP status) chỉ lộ ra khi test thật sự assert nội dung response —
xem `Categories/GetCategoryBySlugTests.cs` (test `Recipes_IncludesPublishedRecipe_InSameCategory`
là test lẽ ra phải fail trước khi vá).

**Bài học:** một handler đọc `entity.NavigationCollection` sau khi gọi repository luôn cần
kiểm tra ngược lại repository có `.Include()` đúng navigation đó không — compiler không giúp
được ở đây vì `IReadOnlyCollection<T>` rỗng và `IReadOnlyCollection<T>` có dữ liệu có cùng kiểu.

---

## D33 — Google OAuth: gộp A1/A2 thành 400, thêm mã 502 cho A3

> Phát hiện lúc lập kế hoạch FR-AUTH-003 (2026-09-23).

**Nguồn mâu thuẫn:** SRS FR-AUTH-003 liệt 3 luồng thay thế: A1 (token Google không hợp lệ
hoặc hết hạn → **401** Unauthorized), A2 (email Google bị revoke quyền → **400** Bad Request),
A3 (Google API không khả dụng → **502** Bad Gateway). D9 đã chốt xác thực bằng ID Token
(`GoogleJsonWebSignature.ValidateAsync`) nhưng chưa nói rõ 3 luồng lỗi này map ra sao. Bảng
"Danh sách Application Error Code" phía trên đã ghi sẵn `AUTH_GOOGLE_TOKEN_INVALID` → **400**
— mâu thuẫn trực tiếp với SRS A1 (401). Hệ thống cũng chưa có exception/HTTP mapping nào cho
502 (`GlobalExceptionMiddleware` hiện chỉ có 400/401/403/404/409/423, còn lại → 500).

**Chốt:**

- **A1 + A2 gộp làm một.** `GoogleJsonWebSignature.ValidateAsync` chỉ có hai kết quả: hợp lệ,
  hoặc ném `InvalidJwtException` — không có tín hiệu riêng để phân biệt "hết hạn" với "bị
  revoke quyền" từ một ID Token đơn lẻ. Mọi lỗi xác thực token (invalid, expired, revoked,
  sai signature, sai audience) → `DomainException(ErrorCodes.AuthGoogleTokenInvalid)` →
  **400**, khớp đúng bảng Error Code đã chốt (không phải 401 như SRS A1 ghi).
- **A3 giữ nguyên 502**, thêm exception mới `BadGatewayException` (cùng pattern với
  `LockedException`), error code mới `AUTH_GOOGLE_UNAVAILABLE`. Dùng khi gọi Google API thất
  bại vì lý do hạ tầng (timeout, DNS, Google trả 5xx) — khác bản chất với lỗi 4xx do client gửi
  token sai. `GlobalExceptionMiddleware` thêm một nhánh map exception này sang 502 — đây là
  mapping 5xx có chủ đích đầu tiên ngoài fallback 500 chung.

**Vì:** ID Token của Google không mang state "revoked" tách biệt khỏi "invalid" — implement A2
như một nhánh riêng là bất khả thi với cơ chế ID Token mà D9 đã chọn (không có gì để phân biệt).
A3 là sự cố hạ tầng thật (Google service down), khác bản chất lỗi do client — giữ 502 giúp
frontend phân biệt "thử lại sau" với "cần đăng nhập lại".

**Sửa SRS:** FR-AUTH-003 — xoá luồng A2 (gộp vào A1), sửa A1 từ "401" thành "400".

---

## D34 — RowVersion trên PostgreSQL: code tự sinh, không dựa vào `[Timestamp]`

> Phát hiện khi rà soát sau PR #25 (2026-09-30). Kế hoạch: `docs/plans/D34-rowversion-concurrency.md`.

**Nguồn mâu thuẫn:** SRS 7.1 ghi `RowVersion` là "bytea (timestamp), NOT NULL, Concurrency Token —
EF Core [Timestamp] annotation", và CONS-006 chốt PostgreSQL là DBMS duy nhất. `[Timestamp]` /
`IsRowVersion()` dựa vào kiểu `rowversion` do **SQL Server** tự tăng sau mỗi lần ghi — PostgreSQL
không có cơ chế tương đương cho cột `bytea`. Kết quả thực tế: `RowVersion` giữ giá trị rỗng mãi,
EF sinh `UPDATE ... WHERE "RowVersion" = ''` luôn đúng, hai người cùng sửa thì người sau âm thầm
ghi đè người trước — không bao giờ có 409 như D4 và FR-RCP-004 A2 yêu cầu.

**Chốt:**

- Giữ cột `RowVersion bytea` đúng SRS (không đổi schema, không migration).
- `Infrastructure/Persistence/RowVersionInterceptor.cs`: trước mỗi `SaveChanges`, mọi `BaseEntity`
  ở trạng thái `Added`/`Modified` — hoặc có owned entity (vd. `Recipe.Nutrition`) thay đổi —
  nhận 16 byte ngẫu nhiên mới. EF vẫn dùng giá trị **gốc** trong mệnh đề WHERE nên nếu người khác
  đã lưu trước → 0 dòng → `DbUpdateConcurrencyException` → **409 `RECIPE_CONCURRENCY_CONFLICT`** (D4).
- Không dùng cột hệ thống `xmin` của PostgreSQL (cách Npgsql khuyến nghị): phải bỏ cột
  `RowVersion`, lệch SRS 7.1 nhiều hơn mà lợi ích không đáng kể ở quy mô dự án.
- Kèm theo: đăng ký `AuditInterceptor` (SRS 7.1 — `CreatedAt`/`UpdatedAt`) vốn đã viết nhưng chưa
  từng được gắn vào DbContext.

**Quy tắc cho FR-RCP-004 (và mọi Command sửa có `If-Match`):** interceptor chỉ chặn được hai request
**chen nhau** giữa lúc đọc và lúc lưu. Để chặn client sửa trên bản đã cũ (đọc từ vài phút trước),
handler PHẢI so `RowVersion` client gửi (header `If-Match`, base64) với `recipe.RowVersion` vừa load —
khác → `ConflictException(ErrorCodes.RecipeConcurrencyConflict)` → 409. Response trả `RowVersion` mới
(header `ETag`) cho lần sửa kế tiếp.

**Vì:** giữ đúng mô hình dữ liệu SRS, cục bộ trong Infrastructure (Domain/Application không đổi),
và có test trên PostgreSQL thật (`IntegrationTests/Common/ConcurrencyTests.cs`).

**Sửa SRS:** mục 7.1 — `RowVersion`: bỏ "EF Core [Timestamp] annotation", thay bằng "giá trị do
ứng dụng sinh mới mỗi lần ghi (xem D34)".

---

## D35 — Refresh token (FR-AUTH-004): mã lỗi, lockout, phạm vi "token family", race

> Phát hiện lúc lập kế hoạch FR-AUTH-004 (2026-09-30). Xem `docs/plans/FR-AUTH-004-lam-moi-token.md`.

**Nguồn mâu thuẫn / thiếu:** SRS FR-AUTH-004 chỉ ghi "401" cho A1–A4, không có error code.
A4 gộp "bị xóa hoặc bị khóa" → 401, trong khi D11 đã chốt `IsActive = false` → 403. A3 ghi
"**có thể** revoke toàn bộ" (tùy chọn), NFR-SEC-002 ghi **bắt buộc** revoke "token family" nhưng
schema 7.8 không có cột family. Bảng 8.1 ghi response `{ accessToken, refreshToken, expiresIn }`
khác `AuthResponseDto` ở Chương 3. Không chỗ nào nói về hai request refresh đồng thời.

**Chốt:**

1. **A1** (RT không có trong DB) và **A4 — user bị xóa** → 401 `AUTH_TOKEN_INVALID`.
2. **A4 — `IsActive = false`** → 403 `AUTH_ACCOUNT_DISABLED` (giữ D11).
3. **Lockout do sai mật khẩu (D17) KHÔNG kiểm tra khi refresh.** Người giữ RT hợp lệ đã chứng
   minh danh tính; nếu kiểm tra, kẻ tấn công chỉ cần cố ý nhập sai 5 lần là đá được chủ tài
   khoản ra khỏi mọi phiên. "Bị khóa" trong A4 hiểu là khóa bởi Admin — đã phủ bởi mục 2.
4. **Thứ tự kiểm tra:** tồn tại → **revoked** → expired → user. Token vừa revoke vừa hết hạn
   vẫn phải kích hoạt reuse detection.
5. **Reuse detection** (NFR-SEC-002 thắng chữ "có thể" của A3): revoke **mọi RT còn hiệu lực
   của user** (không truy chuỗi `ReplacedByTokenHash`) → log WARNING → 401
   `AUTH_REFRESH_TOKEN_REVOKED`. Đơn giản, không cần cột `FamilyId`/migration, và an toàn hơn:
   khi đã có dấu hiệu lộ token thì đăng xuất user trên mọi thiết bị.
6. **Race** (hai request cùng một RT): revoke RT cũ bằng UPDATE có điều kiện
   `RevokedAt IS NULL`. Request thua → 401 `AUTH_REFRESH_TOKEN_REVOKED` nhưng **không** revoke
   family (thường là client mở 2 tab, không phải tấn công).
7. **Response** = `AuthResponseDto` giống login (theo lập luận D24).

**Sửa SRS:** FR-AUTH-004 — A1/A4 ghi rõ mã lỗi như trên, tách "bị khóa" thành 403 (D11); A3
đổi "có thể" thành "phải revoke mọi RT còn hiệu lực của user"; bảng 8.1 dòng `/auth/refresh`
đổi response thành `AuthResponseDto`.

---

## D36 — OpenTelemetry: đích export theo môi trường

> FR-OBS-003 (2026-09-30). Kế hoạch: `docs/plans/FR-OBS-003-tracing-metrics.md`. D35 đã được giữ
> cho FR-AUTH-004 (A).

**Nguồn mâu thuẫn:** SRS 3.7 (FR-OBS-003) ghi "Traces được export đến Seq (development)" và mục
tích hợp ghi "Development: Seq OTLP", nhưng cùng FR đó yêu cầu metrics (request count, duration,
error rate) mà **Seq không ingest metrics** — chỉ nhận OTLP logs và traces. Bảng tích hợp 2.x còn
ghi Collector nhận "OTLP / gRPC", trong khi Seq nhận OTLP qua **HTTP/protobuf**.

**Chốt:**

- Mỗi signal (trace, metric) có exporter OTLP riêng, **chỉ đăng ký khi có endpoint** cấu hình
  (`OTEL_EXPORTER_OTLP_ENDPOINT` hoặc biến riêng từng signal). Không có endpoint → instrumentation
  vẫn chạy (TraceId vẫn vào log) nhưng không gửi đi đâu. Môi trường `Testing` không export.
- **Development:** trace → Seq `http://localhost:5341/ingest/otlp/v1/traces`, protocol
  `http/protobuf`. Metric **không export** (xem tại chỗ bằng `dotnet-counters` nếu cần).
- **Production:** trace + metric → OTel Collector qua `OTEL_EXPORTER_OTLP_ENDPOINT` (gRPC mặc định),
  Collector chuyển tiếp tới Grafana Tempo/Jaeger và Prometheus/Grafana.

**Vì:** giữ đúng ý SRS (dev xem trace trong Seq cạnh log, lọc theo TraceId) mà không gửi metric vào
một đích trả lỗi liên tục.

**Sửa SRS:** FR-OBS-003 — "Traces được export đến Seq (development)" thêm "; metrics chỉ export ở
production (Seq không nhận metrics)". Bảng tích hợp — Seq nhận OTLP qua HTTP/protobuf.

---

## D37 — "EF Core database traces" dùng `Npgsql.OpenTelemetry`

> FR-OBS-003 (2026-09-30).

**Nguồn mâu thuẫn:** SRS 3.7 yêu cầu "EF Core database operation traces" nhưng không nêu gói.
`OpenTelemetry.Instrumentation.EntityFrameworkCore` vẫn ở bản **beta**; NFR-MAINT yêu cầu phụ thuộc
ổn định.

**Chốt:** dùng **`Npgsql.OpenTelemetry`** (`TracerProviderBuilder.AddNpgsql()`), bản stable cùng
phiên bản với driver Npgsql. Mỗi câu lệnh SQL mà EF Core gửi xuống là một span (`db.statement` đã
tham số hóa — **không chứa giá trị tham số**, không lộ dữ liệu người dùng hay hash token).

**Vì:** CONS-006 chốt PostgreSQL là DBMS duy nhất nên instrumentation ở tầng driver phủ đủ mọi truy
vấn EF Core (và cả Hangfire.PostgreSql), không cần gói beta.

**Sửa SRS:** FR-OBS-003 — "EF Core database operation traces" → "database operation traces
(Npgsql instrumentation)".

---

## D38 — Định nghĩa "error rate"

> FR-OBS-003 (2026-09-30).

**Nguồn mâu thuẫn:** SRS 3.7 liệt kê metric "error rate" nhưng không định nghĩa lỗi là gì.

**Chốt:** theo OpenTelemetry semantic convention của ASP.NET Core — request **lỗi** là request có
thuộc tính `error.type` trên histogram `http.server.request.duration`: status **5xx** hoặc exception
chưa xử lý. **4xx không tính là lỗi server** (400 validation, 401 token sai, 404…). API không tự
tính tỉ lệ; backend quan sát (Grafana) tính từ histogram có sẵn của ASP.NET Core.

**Vì:** 4xx là lỗi của client, đưa vào error rate sẽ làm cảnh báo kêu mỗi khi có người gõ sai mật khẩu.

**Sửa SRS:** FR-OBS-003 — "error rate" thêm "(tỉ lệ response 5xx)".

---

## D39 — Metric nghiệp vụ recipe created/published: tách phần đo và phần gọi

> FR-OBS-003 (2026-09-30).

**Nguồn mâu thuẫn:** FR-OBS-003 (D, slice S11) yêu cầu metric "recipe created/published count",
nhưng handler tạo và publish recipe thuộc FR-RCP-003/005 (C, slice S5/S7) — chưa tồn tại lúc làm
FR-OBS-003, và `Application/Recipes/Commands/**` do C sở hữu.

**Chốt:**

- D tạo `Application/Common/Observability/RecipeMetrics.cs` (meter `CulinaryBlog.Recipes`, counter
  `culinaryblog.recipes.created`, `culinaryblog.recipes.published`) — chỉ dùng
  `System.Diagnostics.Metrics` (BCL), không reference OpenTelemetry trong Application (CONS-001).
- C gọi `RecordCreated()` trong handler FR-RCP-003 và `RecordPublished()` trong handler FR-RCP-005,
  **sau khi `SaveChangesAsync` thành công** (không đếm lần publish bị D3 chặn hay lỗi DB).
- FR-OBS-003 để trạng thái 🟡 trong `traceability.md` cho tới khi hai lời gọi đó có mặt.

**Vì:** không sửa file của người khác, không chặn FR-OBS-003 chờ slice S5/S7.

**Sửa SRS:** không cần.

---

## D40 — FR-JOB-002: cách resize ảnh

> Phát hiện lúc lập kế hoạch FR-JOB-002 (2026-10-07) — xem `docs/plans/FR-JOB-002-resize-anh.md`.

**Nguồn:** FR-JOB-002 chỉ ghi "thumbnail (300x300px) và medium image (800x600px)". Ảnh gốc có tỉ
lệ bất kỳ; SRS không nói crop hay fit, có upscale ảnh nhỏ không.

**Chốt:**

- **Thumbnail:** crop giữa ảnh, ra **đúng 300×300** (ô vuông cho card/lưới).
- **Medium:** thu cho **lọt trong 800×600, giữ tỉ lệ, không crop** (ảnh gallery không được mất nội dung).
- **Không upscale:** cạnh nguồn nhỏ hơn đích thì giữ nguyên kích thước nguồn (thumbnail vẫn crop vuông).
- Xoay ảnh theo EXIF Orientation trước khi resize, rồi **bỏ toàn bộ metadata** (EXIF/GPS/ICC
  không cần thiết) — ảnh chụp điện thoại thường mang tọa độ GPS.
- Giới hạn kích thước decode (40 megapixel) để chống decompression bomb — file ≤ 5 MB (CONS-007)
  vẫn có thể giải nén ra hàng GB.

**Vì:** hành vi phổ biến nhất cho blog ảnh; không upscale vì phóng to chỉ làm file nặng hơn mà
không nét hơn.

**Sửa SRS:** FR-JOB-002 — bổ sung "thumbnail crop vuông, medium fit giữ tỉ lệ, không upscale".

---

## D41 — FR-JOB-002: định dạng ảnh phái sinh, AVIF bị bỏ qua

> 2026-10-07.

**Nguồn:** FR-JOB-002 không nói định dạng đầu ra. D28 cho phép upload AVIF, nhưng
**SixLabors.ImageSharp 3.1.12** (thư viện đã có trong `Infrastructure.csproj`) **không có decoder
AVIF** — đã kiểm tra: chỉ có Bmp, Gif, Jpeg, Pbm, Png, Qoi, Tga, Tiff, Webp.

**Chốt:**

- Ảnh medium và thumbnail luôn mã hóa **WebP**, quality 80 — một encoder duy nhất, nhẹ hơn JPEG/PNG,
  mọi trình duyệt hiện hành đều hỗ trợ.
- Ảnh gốc **AVIF → job kết thúc ngay (no-op)**, log Information. `MediumUrl`/`ThumbnailUrl` giữ
  `null`, FE dùng `originalUrl` — đúng nhánh "nếu fail: ảnh gốc vẫn hiển thị" của FR-JOB-002.
  Không retry (lỗi không tự hết sau khi chờ).

**Vì:** thêm thư viện native (libvips/ImageMagick) chỉ để đọc AVIF làm nặng image Docker và CI,
lợi ích nhỏ — AVIF vốn đã là định dạng nén tốt.

**Sửa SRS:** FR-JOB-002 — "ảnh phái sinh định dạng WebP; ảnh gốc AVIF không sinh phiên bản phụ".

---

## D42 — FR-JOB-002: lịch retry

> 2026-10-07.

**Nguồn:** FR-JOB-002 ghi "Retry 3 lần" không có khoảng chờ; FR-JOB-001 có lịch 1 / 5 / 30 phút;
NFR-REL chỉ nói "exponential backoff".

**Chốt:** `[AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded =
AttemptsExceededAction.Fail)]`. Hết lượt → job ở trạng thái Failed (xem được ở `/hangfire`) + log Error.

**Vì:** thống nhất một lịch retry cho mọi fire-and-forget job của dự án.

**Sửa SRS:** FR-JOB-002 — bổ sung lịch retry 1 / 5 / 30 phút.

---

## D43 — Xóa ảnh: xóa cả file phái sinh

> 2026-10-07.

**Nguồn:** FR-FILE-002 và FR-RCP-008 bước 14 chỉ nói "xóa file trên MinIO" (số ít) — viết trước
khi có FR-JOB-002 sinh thêm 2 file cho mỗi ảnh.

**Chốt:** `DELETE /recipes/{id}/images/{imageId}` enqueue `FR-FILE-002` cho **từng URL khác null**
trong `OriginalUrl`, `MediumUrl`, `ThumbnailUrl`. Vẫn **không** áp dụng khi xóa cả recipe (D1).

Nếu job resize đang chạy dở mà ảnh bị xóa: job thấy ảnh không còn ở bước ghi DB → tự enqueue xóa
2 file vừa upload (D44), không để lại file mồ côi.

**Sửa SRS:** FR-FILE-002, FR-RCP-008 bước 14 — "xóa file gốc và các phiên bản resize".

---

## D44 — FR-JOB-002: luồng job đi qua MediatR, idempotent

> 2026-10-07.

**Nguồn:** ghi chú FR-JOB-002 trong `traceability.md` (2026-09-22) mô tả việc "cập nhật DB ngoài
luồng MediatR" — đi ngoài pipeline thì không xóa cache (D8) và lệch CONS-002.

**Chốt:**

- Hangfire job (`Infrastructure/Jobs/ResizeRecipeImageJob`) chỉ làm một việc:
  `sender.Send(new GenerateRecipeImageVariantsCommand(imageId))`. Logic nằm trong handler ở
  Application; command implement `ICacheInvalidator` (tag `recipes`, `recipe:{slug}`).
- Application không biết Hangfire: enqueue qua `IBackgroundJobService.EnqueueGenerateImageVariants`.
  Upload chỉ enqueue **sau khi transaction ghi ảnh thành công**; enqueue lỗi → log warning, không
  làm fail request (ảnh gốc vẫn dùng được).
- File phái sinh lưu `recipes/{recipeId}/{guid}.webp` qua `ObjectKey` có sẵn (tên GUID, D16).
- **Idempotent:** ảnh không còn / recipe đã soft delete / ảnh đã có `ThumbnailUrl` → no-op.
  Lỗi sau khi đã upload file phái sinh → enqueue xóa 2 file đó rồi ném lại để Hangfire retry.
- Không check ownership — lệnh hệ thống, không có endpoint gọi tới.

**Sửa SRS:** không cần (chi tiết hiện thực).

---

## D45 — Bật lại Hangfire server

> 2026-10-07. Ảnh hưởng FR-JOB-001 (A), FR-FILE-002, FR-JOB-002, FR-JOB-003.

**Hiện trạng:** `builder.Services.AddHangfireServer()` trong `Program.cs` đang bị comment → job
được ghi vào schema `hangfire` nhưng **không worker nào chạy**: welcome email không gửi, file
MinIO của ảnh đã xóa không bị xóa.

**Lịch sử (git):** `4b78dfb` (2026-09-26) xóa `AddHangfire(...)` khỏi `DependencyInjection.cs` và
thay service thật bằng Mock — server không còn storage nên phải comment để host lên được.
`1d5fad9` (2026-09-28) khôi phục `AddHangfire` nhưng không bật lại server, coi trạng thái tạm đó
là thiết kế.

**Chốt:** bỏ comment `AddHangfireServer()`, giữ điều kiện `!IsEnvironment("Testing")`.

**Vì:** SRS mục 2.x ghi Hangfire chạy in-process; lo ngại "host không lên khi Postgres chưa sẵn
sàng" không đáng kể — SRS ghi Postgres down là "toàn bộ hệ thống ngừng" (readiness fail, Nginx
503), và NFR-REL-002 nói về Redis chứ không phải Postgres. `SmokeTests`/integration test chạy
`Testing` nên không bị ảnh hưởng.

**Lưu ý nhóm:** `Program.cs` là file dùng chung — báo A và Yen trước khi merge; bật server thì
welcome email bắt đầu gửi thật.

**Sửa SRS:** không cần.

---

## D46 — FR-JOB-002: thay ImageSharp bằng SkiaSharp

> 2026-10-10. Phát hiện sau khi merge PR #37: CI đỏ ở bước `dotnet restore`.

**Nguồn:** NuGetAudit báo `SixLabors.ImageSharp` 3.1.12 dính 5 advisory (GHSA-gwg2-r3hj-4w44,
GHSA-j3p4-wp97-rph4, GHSA-j9gm-c75j-xc9q, GHSA-jjfr-hcj7-qf5w, GHSA-wmxv-xphr-5c9g — 3 high,
2 moderate); `TreatWarningsAsErrors` biến NU1902/NU1903 thành lỗi. Cả 5 chỉ vá từ **4.1.2**, nhưng
ImageSharp 4.x **bắt buộc license key Six Labors** — build Release (CI, Dockerfile) lỗi nếu thiếu.

**Chốt:** dùng **SkiaSharp 3.119.4** (MIT) + `SkiaSharp.NativeAssets.Linux.NoDependencies` cho
CI/Docker. `ImageSharpImageResizer` → `SkiaImageResizer`. D40 và D41 giữ nguyên hành vi: native
Skia đóng gói sẵn **không có decoder AVIF** (đã kiểm tra binary win-x64 và linux-x64), nên ảnh gốc
AVIF vẫn no-op như D41.

**Vì:** không phải xin/giữ license key trong secret của CI và Docker; không suppress lỗ hổng thật
(GHSA-gwg2 nằm ở đường parse ICC — ảnh JPEG upload chạm tới được). Lý do kỹ thuật chi tiết: ADR-0004.

**Sửa SRS:** không cần.
## D47 — Đăng xuất (FR-AUTH-005): access token, token của user khác, token đã revoke

> Phát hiện lúc lập kế hoạch FR-AUTH-005 (2026-10-07). Xem `docs/plans/FR-AUTH-005-dang-xuat-thu-hoi-token.md`.

**Nguồn mâu thuẫn / thiếu:** SRS FR-AUTH-005 điều kiện tiên quyết và bảng status yêu cầu access token
hợp lệ (401 nếu không), nhưng A2 lại cho logout khi "access token đã hết hạn" và trả 401 nếu thiếu
refresh token. Bước 4 chỉ revoke khi token thuộc user hiện tại, không nói trường hợp ngược lại.

**Chốt:**

1. **Giữ `RequireAuthorization()`** (khớp điều kiện tiên quyết, bảng status, NFR-SEC-006). **Bỏ A2.**
   Frontend tự refresh trước nếu access token sắp hết hạn rồi mới gọi logout.
   Thiếu / rỗng `refreshToken` → **400** `VALIDATION_ERROR` (D4), không phải 401.
2. **Token thuộc user khác** → 204, **không revoke**, log WARNING (giống A1, không lộ trạng thái token).
3. **Token đã revoke hoặc hết hạn** → 204 no-op; **không** kích hoạt reuse detection (D35-5 chỉ dành
   cho `/refresh`).
4. **Race** → `TryRevokeAsync` (UPDATE có điều kiện `RevokedAt IS NULL`, D35-6); thua race vẫn 204.

**Vì:** logout idempotent không được biến thành kênh dò trạng thái token hay công cụ đăng xuất người khác.

**Sửa SRS:** FR-AUTH-005 — bỏ A2; bảng status thêm 400; ghi rõ token của user khác → 204 (không revoke).

---

## D48 — Xem hồ sơ (FR-AUTH-006): user bị xóa, user bị vô hiệu hóa

> Phát hiện lúc lập kế hoạch FR-AUTH-006 (2026-10-07). Xem `docs/plans/FR-AUTH-006-xem-ho-so-ca-nhan.md`.

**Nguồn mâu thuẫn / thiếu:** SRS FR-AUTH-006 A1 ghi user đã bị xóa → 404, nhưng D35 chốt `/refresh`
trả 401 `AUTH_TOKEN_INVALID` cho cùng tình huống và bảng mã lỗi chưa có mã 404 cho user. SRS cũng không
nói user `IsActive=false` còn access token gọi `/me`.

**Chốt:**

1. User đã bị xóa khỏi DB sau khi token được cấp → **404** `USER_NOT_FOUND` (giữ SRS A1). Khác `/refresh`
   vì `/me` đã qua JWT hợp lệ, còn `/refresh` xác thực bằng chính refresh token.
2. User `IsActive=false` còn access token → **200** (chỉ đọc; chặn đã làm ở login và refresh theo D11).
3. Thêm `USER_NOT_FOUND` (404) vào bảng mã lỗi.

**Vì:** `/me` chỉ đọc hồ sơ của chính chủ, không cấp thêm quyền nào.

**Sửa SRS:** FR-AUTH-006 — thay `fullName/userName/emailConfirmed/createdAt` theo D5/D12; ghi mã lỗi 404.

---

## D49 — Full-text search (FR-SRCH-001): cấu hình, SearchVector, xếp hạng

> 2026-10-10. Làm lại FR-SRCH-001 sau review PR #39.

**Nguồn mâu thuẫn / thiếu:** SRS FR-SRCH-001 bước 4 dùng `ToTsQuery("vietnamese", …)`, nhưng PostgreSQL
16 **không có** cấu hình text search `vietnamese` (chỉ có các ngôn ngữ đi kèm Snowball) → lỗi ngay khi
chạy. SRS gọi `SearchVector` là "computed column" nhưng lại nói "được cập nhật bởi trigger"; cột sinh
(`GENERATED ALWAYS`) không dùng được `unaccent()` vì hàm này không `IMMUTABLE`. SRS không nói Title và
Description có quan trọng ngang nhau không.

**Chốt:**

1. Cấu hình **`simple` + `unaccent`** cho cả vector lẫn query: tách từ theo khoảng trắng, chữ thường,
   bỏ dấu ("pho" tìm ra "phở", "dau" tìm ra "đậu"). Không stemming — tiếng Việt không biến hình từ.
2. `Recipes."SearchVector"` (`tsvector`) do **trigger** `trg_recipes_search_vector` (BEFORE INSERT OR
   UPDATE OF Title, Description) tính qua hàm `recipe_search_vector(title, description)`; migration
   `AddRecipeSearchVector` điền sẵn cho dữ liệu cũ. **GIN index** `IX_Recipes_SearchVector`.
3. Trọng số: **Title = A, Description = B** → khớp ở tiêu đề xếp trên khớp ở mô tả.
4. Lọc `SearchVector @@ to_tsquery('simple', unaccent('từ1:* & từ2:*'))` (tiền tố, mọi từ đều phải có);
   xếp `ts_rank` giảm dần, hòa điểm thì mới hơn trước. Trả `relevanceScore` trong `RecipeSummaryDto`
   (tùy chọn — danh sách thường để `null`).
5. **Bỏ** nhánh khớp `ILIKE '%từ%'` có trước đây: nó tìm cả chuỗi con giữa từ (ngoài SRS) và buộc quét
   toàn bảng, vô hiệu hóa GIN index.
6. Trong EF, `SearchVector` là **shadow property** (CONS-001: Domain không phụ thuộc `NpgsqlTsVector`),
   EF không bao giờ ghi cột này.
7. Không bật `pg_trgm`: FTS không dùng tới. Bật khi có tính năng cần (tìm gần đúng theo trigram).
8. Query rỗng / < 2 ký tự → **400** (D4, không phải 422). Cache 1 phút (D8).

**Vì:** dùng đúng cơ chế SRS mô tả (trigger + GIN + ts_rank) với cấu hình chạy được trên PostgreSQL thật.

**Sửa SRS:** FR-SRCH-001 — `vietnamese` → `simple` + `unaccent`; "computed column" → "cột cập nhật bởi
trigger"; ghi trọng số Title A / Description B; mã lỗi 422 → 400 (D4).

---

## D50 — Phân quyền lệnh ghi Category ở Application Layer (NFR-SEC-006)

> 2026-10-13. Review PR #49.

**Nguồn thiếu:** NFR-SEC-006 yêu cầu kiểm tra phân quyền ở Application Layer, không chỉ ở Presentation
Layer. FR-CAT-003/004/005 chỉ cho Admin, endpoint đã có `RequireAuthorization("AdminPolicy")`, nhưng
handler chưa kiểm tra lại. Bảng Application Error Code chỉ có `RECIPE_FORBIDDEN` (403), không có mã 403
nào cho Category.

**Chốt:**

1. `CreateCategoryCommandHandler`, `UpdateCategoryCommandHandler`, `DeleteCategoryCommandHandler` kiểm tra
   `ICurrentUser.IsAdmin` **trước mọi truy cập dữ liệu**. Không phải Admin → `ForbiddenException`
   → **403** `CATEGORY_FORBIDDEN`.
2. Thêm mã `CATEGORY_FORBIDDEN` (403), cùng dạng với `RECIPE_FORBIDDEN`.
3. Không dùng `UnauthorizedAccessException` của BCL: `GlobalExceptionMiddleware` không map kiểu này,
   nên nó sẽ thành 500.
4. Ở endpoint, `AdminPolicy` vẫn chặn trước (trả 403 của ASP.NET). Kiểm tra trong handler là lớp thứ hai,
   phòng khi endpoint bị cấu hình thiếu policy hoặc command được gửi từ nơi khác (job, endpoint mới).

**Vì:** đúng NFR-SEC-006, và lỗi phân quyền luôn trả RFC 7807 với error code, không bao giờ 500.

**Sửa SRS:** bảng Application Error Code — thêm `CATEGORY_FORBIDDEN` (403, Category).

---

## D51 — FR-JOB-003: bỏ bước ping Google Search Console

> 2026-10-14.

**Nguồn:** SRS FR-JOB-003 ("Gửi thông báo đến Google Search Console (ping)"), NFR-SEO-003 ("Ping Google
Search Console sau khi update sitemap"), bảng tích hợp ngoài (`GET https://www.google.com/ping?sitemap={url}`).

**Mâu thuẫn với thực tế:** Google đã ngừng endpoint ping sitemap từ 06/2023; từ cuối 2023 gọi vào trả
404. SRS yêu cầu tích hợp với một dịch vụ không còn tồn tại.

**Chốt:** không gọi ping. Google phát hiện sitemap qua dòng `Sitemap: {site}/sitemap.xml` trong
`robots.txt` (đã là yêu cầu của NFR-SEO-003) và qua khai báo một lần trong Search Console.

**Vì:** gọi một URL chắc chắn 404 mỗi ngày chỉ sinh log lỗi giả; để lỗi đó làm fail job thì sitemap không
bao giờ "thành công".

**Sửa SRS:** FR-JOB-003 (bỏ câu ping), NFR-SEO-003 (bỏ câu ping), bảng tích hợp ngoài (bỏ dòng Google
Search Console).

---

## D52 — FR-JOB-003: sitemap.xml lưu trên MinIO, Nginx phục vụ

> 2026-10-14.

**Nguồn:** SRS FR-JOB-003 "Upload sitemap.xml lên MinIO **hoặc** lưu vào wwwroot" — chưa chốt.
`team-assignment.md` lại giao `frontend/app/sitemap.ts` — thành hai nơi cùng sinh sitemap.
`IFileStorageService.UploadAsync` luôn sinh tên GUID (D16), không ghi được key cố định.

**Chốt:**

1. Job ghi lên MinIO, key cố định **`seo/sitemap.xml`**, `Content-Type: application/xml`, ghi đè mỗi lần chạy.
2. Thêm `IFileStorageService.SaveAsync(objectKey, content, contentType)` — **chỉ cho file do hệ thống
   sinh ra**. File người dùng upload vẫn đi `UploadAsync` (tên GUID, magic bytes — D16, D28, CONS-007).
3. Nginx: `location = /sitemap.xml` proxy tới object trên MinIO (bucket public-read, D16).
4. **Không** làm `frontend/app/sitemap.ts`. Frontend chỉ có `app/robots.ts`.
5. Danh sách URL: `/`, `/recipes`, `/categories`, mọi category chưa xóa, mọi recipe Published. Không đưa
   `/search` (trang kết quả rỗng khi không có từ khóa), `/auth/*`, `/dashboard/*`.
6. Base URL lấy từ cấu hình `Seo:SiteBaseUrl`; thiếu → job lỗi (Hangfire retry rồi Failed).

**Vì:** wwwroot làm mỗi instance API giữ một bản riêng — lệch yêu cầu stateless/scale ngang (SRS mục
NFR-SCAL). Sinh ở Next.js thì không còn là Hangfire job như FR-JOB-003.

**Sửa SRS:** FR-JOB-003 — "Upload sitemap.xml lên MinIO (key `seo/sitemap.xml`), Nginx phục vụ tại `/sitemap.xml`".

---

## D53 — FR-JOB-003: Hangfire `DisableConcurrentExecution` thay RedLock

> 2026-10-14.

**Nguồn:** SRS mục khả năng mở rộng: "Distributed lock (RedLock) cho các tác vụ singleton (sitemap generation)".

**Chốt:** `SitemapGenerationJob` gắn `[DisableConcurrentExecution(timeoutInSeconds: 600)]`. Lock nằm trên
Hangfire PostgreSQL storage — vốn đã chia sẻ giữa mọi worker, nên là lock phân tán. Recurring job của
Hangfire cũng chỉ được enqueue **một lần** cho mỗi mốc lịch dù có nhiều server.

**Vì:** không thêm thư viện; RedLock phụ thuộc Redis, mà Redis down không được làm hỏng tác vụ
(NFR-REL-002) — sitemap sẽ không sinh được đúng lúc Redis gặp sự cố.

**Sửa SRS:** mục khả năng mở rộng — "Distributed lock (Hangfire `DisableConcurrentExecution`) cho tác vụ singleton".

---

## D54 — Cập nhật hồ sơ (FR-AUTH-007): PATCH một phần

> Phát hiện lúc lập kế hoạch FR-AUTH-007 (2026-10-14). Xem `docs/plans/FR-AUTH-007-cap-nhat-ho-so-ca-nhan.md`.

**Nguồn mâu thuẫn / thiếu:** SRS FR-AUTH-007 dùng `fullName` và 422 (D5, D4 đã thay). SRS không nói body rỗng,
giá trị `null`, cách xóa `bio`/`avatarUrl`, user `IsActive=false` hay user đã bị xóa khi gọi PATCH.

**Chốt:**

1. Body không có field nào (`{}`) → **400** `VALIDATION_ERROR`.
2. Field vắng hoặc `null` → **giữ nguyên**. Xóa `bio`/`avatarUrl` bằng chuỗi rỗng `""` (server lưu `NULL`).
3. `displayName` rỗng/chỉ khoảng trắng → 400 (D5: NOT NULL, 2–100 ký tự). `avatarUrl` phải là URL tuyệt đối http/https.
4. User `IsActive=false` còn access token → **200** (không cấp thêm quyền; D11 chặn ở login/refresh).
5. User đã bị xóa → **404** `USER_NOT_FOUND` (D48).
6. `email` và `userName` trong body (nếu có) bị bỏ qua, không đổi được.

**Vì:** record C# với `string?` không phân biệt "vắng" và "null"; phân biệt cần tự dò JSON, không cần cho v1.

**Sửa SRS:** FR-AUTH-007 — thay `fullName` theo D5, thay 422 bằng 400 (D4), bổ sung các điểm trên.

---

## D55 — FR-JOB-001: enqueue welcome email lỗi không làm fail request

> 2026-10-21. Xem `docs/plans/FR-JOB-001-welcome-email.md`.

**Nguồn:** FR-AUTH-001 bước 11 chỉ nói "đẩy email chào mừng vào Hangfire queue", không nói enqueue lỗi
thì sao. Code hiện tại gọi `EnqueueWelcomeEmail` sau `SaveChanges` không bắt lỗi → Hangfire storage lỗi
thì user đã được tạo nhưng client nhận 500 (đăng ký lại sẽ bị 409 email trùng).

**Chốt:** `RegisterCommandHandler` và `GoogleLoginCommandHandler` bọc lời gọi enqueue trong try/catch →
log **Warning** (kèm `UserId`, không ghi email — CONS-010), vẫn trả `AuthResponseDto` bình thường (201 / 200).

**Vì:** SRS mục rủi ro phụ thuộc: Hangfire hỏng "không ảnh hưởng core functionality". Cùng cách đã chốt
cho ảnh ở D44 ("enqueue lỗi → log warning, không làm fail request").

**Sửa SRS:** FR-AUTH-001 — bổ sung luồng ngoại lệ "enqueue welcome email lỗi → vẫn đăng ký thành công".

---

## D56 — FR-JOB-001: ai nhận welcome email

> 2026-10-21.

**Nguồn:** SRS FR-JOB-001 ghi trigger "Sau FR-AUTH-001 thành công". FR-AUTH-003 (Google) cũng tự tạo
tài khoản mới nhưng không nhắc email chào mừng. Roadmap S9 gộp FR-AUTH-003 và FR-JOB-001.

**Chốt:** gửi khi **tạo tài khoản mới**: đăng ký email/mật khẩu (FR-AUTH-001) và đăng nhập Google lần
đầu tạo user mới (FR-AUTH-003, `isNewUser = true`). Google liên kết vào tài khoản đã có, hoặc đăng nhập
lại → **không** gửi.

Google không trả claim `name` (thiếu scope `profile`) → `displayName` của tài khoản mới là phần trước
`@` của email. Nếu để rỗng, `SendWelcomeEmailCommandValidator` chặn và job retry vô ích rồi Failed.

**Vì:** với người dùng, cả hai cách đều là "đăng ký". Gửi theo `isNewUser` đảm bảo mỗi tài khoản nhận
đúng một email.

**Sửa SRS:** FR-JOB-001 — trigger "Sau FR-AUTH-001 thành công, hoặc FR-AUTH-003 tạo tài khoản mới".

---

## D57 — FR-JOB-001: nội dung email và cấu hình SMTP

> 2026-10-21.

**Nguồn:** SRS FR-JOB-001 "link đến ứng dụng" không nói URL lấy từ đâu; bảng tích hợp ngoài có
`Smtp__Username/Password` và "SMTP với TLS" nhưng Mailhog (dev) không có TLS lẫn auth.

**Chốt:**

1. Link trong email: `{Seo:SiteBaseUrl}/` và `{Seo:SiteBaseUrl}/dashboard` — dùng lại key của FR-JOB-003,
   không thêm key mới. Base URL đi qua command; validator yêu cầu URL tuyệt đối http(s) → thiếu/sai thì
   `ValidationException`, Hangfire retry rồi Failed (giống D52).
2. `displayName` do người dùng nhập → **luôn HTML-encode** khi đưa vào template.
3. Email **multipart/alternative**: phần HTML + phần plain text.
4. SMTP: có `Smtp:Username` thì `AuthenticateAsync`; TLS `SecureSocketOptions.Auto` (STARTTLS khi server
   hỗ trợ, Mailhog thì không).
5. Nội dung email dựng ở Application (`WelcomeEmailTemplate`); `IEmailService` ở Infrastructure chỉ gửi
   `EmailMessage`. Job đi qua MediatR (`SendWelcomeEmailCommand`) như D44.

**Vì:** một nguồn cấu hình cho URL công khai của site; chặn chèn HTML vào email; chạy được cả Mailhog
lẫn SMTP production mà không đổi code.

**Sửa SRS:** FR-JOB-001 — "link về trang chủ và `/dashboard`, gốc URL theo cấu hình site".

---

## D58 — CONS-009: URL công khai của ảnh MinIO

> 2026-10-21. Xem `docs/plans/CONS-009-docker-compose-prod.md`.

**Nguồn:** SRS §6.5 chạy MinIO trong mạng Docker (`http://minio:9000`), nhưng `MinioFileStorageService`
ghép URL ảnh trả cho trình duyệt từ chính `Minio:Endpoint` — địa chỉ nội bộ đó trình duyệt không mở được.
SRS không nói ảnh được phục vụ ra Internet bằng địa chỉ nào.

**Chốt:**

1. Thêm cấu hình `Minio:PublicBaseUrl`. Có giá trị → URL ảnh = `{PublicBaseUrl}/{bucket}/{key}`;
   rỗng → giữ hành vi cũ (`Minio:Endpoint`), dev không đổi (`http://localhost:9000`).
2. Prod: `PublicBaseUrl = https://{domain}`. Nginx `location /culinary-blog/` proxy tới MinIO, chỉ cho GET/HEAD.
3. Tách key từ URL vẫn theo `/{bucket}/` → xóa/resize được ảnh dù URL ghép từ địa chỉ nào.
4. Server Next.js (SSR/ISR và tối ưu ảnh `next/image`) gọi chính domain của site: trong compose, container
   `nginx` có network alias = `SITE_DOMAIN`, frontend tin chứng chỉ qua `NODE_EXTRA_CA_CERTS`. Vì domain đó
   phân giải ra IP nội bộ, bật `images.dangerouslyAllowLocalIP` — `remotePatterns` chỉ cho phép host của
   chính dự án (`localhost:9000`, domain site) nên không mở thêm đường SSRF.

**Vì:** ảnh cùng origin với site → không cần domain/chứng chỉ riêng cho MinIO, MinIO không lộ ra Internet (D60).

**Sửa SRS:** §6.5 — thêm "ảnh MinIO phục vụ qua Nginx tại `/{bucket}/`".

---

## D59 — CONS-009: migration và seed role ở Production

> 2026-10-21.

**Nguồn:** SRS không nói migration chạy lúc nào ở prod. `Program.cs` chỉ migrate + seed role trong
`IsDevelopment()` → DB prod trống bảng, register/login lỗi 500 (thiếu role — xem `IdentityRoleSeeder`).

**Chốt:**

1. API nhận tham số `--migrate-only`: migrate + seed 2 role (`Author`, `Admin`) rồi thoát, không mở cổng.
2. `docker-compose.prod.yml` có service `migrate` (cùng image API) chạy lệnh đó một lần; `api`
   `depends_on: migrate: condition: service_completed_successfully`.
3. **Không** chạy `DbSeeder` (dữ liệu mẫu) ở prod.
4. Development dùng chung hàm `MigrateAndSeedRolesAsync`, rồi chạy thêm `DbSeeder` như cũ.

**Vì:** migrate lúc API khởi động thì khi scale nhiều instance, các instance cùng migrate một lúc.
Tách thành bước riêng cũng làm lỗi migration hiện rõ (service `migrate` exit ≠ 0, `api` không lên).

**Sửa SRS:** §6.5 — thêm bước migration trước khi API khởi động.

---

## D60 — CONS-009: chỉ Nginx publish cổng ở Production

> 2026-10-21.

**Nguồn:** bảng service SRS §6.5 ghi cổng 5432/6379/9000/8080/3000 cho cả production — publish ra host
nghĩa là DB, Redis, MinIO mở ra Internet, và API đi vòng được qua Nginx (bỏ qua HTTPS — NFR-SEC-005).

**Chốt:**

1. Prod chỉ `nginx` publish `80` và `443`. Các service còn lại chỉ nằm trong mạng nội bộ của compose.
2. API bật `UseForwardedHeaders` (`X-Forwarded-For`, `X-Forwarded-Proto`) ở đầu pipeline và **xóa danh
   sách proxy tin cậy mặc định** (chỉ có loopback) — địa chỉ của Nginx trong mạng Docker thay đổi mỗi lần tạo
   lại container. An toàn vì API không nhận kết nối nào ngoài Nginx (mục 1).
3. HSTS của API đặt `max-age=31536000` (giống Nginx, NFR-SEC-005).

**Vì:** NFR-SEC-005 (mọi traffic qua HTTPS) và nguyên tắc không lộ hạ tầng. Thiếu forwarded headers thì
sau Nginx scheme luôn là `http` (HSTS không gửi) và IP client trong log luôn là IP của Nginx (CONS-010).

**Sửa SRS:** §6.5 — cột cổng của production chỉ còn Nginx 80/443.

---

## D61 — NFR-SEO-001/002: og:image, meta description, tiêu chí Rich Results

> 2026-10-28. Xem `docs/plans/NFR-SEO-001-002-seo-frontend.md`.

**Nguồn:** NFR-SEO-002 đòi og:image **1200×630** nhưng FR-JOB-002 chỉ sinh 800×600 và 300×300; đòi meta
description **150–160 ký tự** nhưng description là nội dung tác giả nhập, dài ngắn tùy ý. NFR-SEO-001 đòi
Rich Results "pass 100%" và nhắc "star rating" — trong khi rating nằm ngoài scope v1. SRS không nói trang
nào ngoài recipe được index, cũng không nói canonical xử lý query param thế nào.

**Chốt:**

1. og:image của `/recipes/{slug}` sinh ở frontend bằng Route Handler
   `app/(public)/recipes/[slug]/og-image/route.tsx` (`next/og`), URL cố định `/recipes/{slug}/og-image`,
   khai báo trong `openGraph.images` + `twitter.images` (kèm 1200×630) và trong `image` của JSON-LD:
   ảnh chính crop cover + tên món + tên site. Không có ảnh, hoặc ảnh không đọc được (WebP/AVIF, lỗi
   mạng — nhận diện bằng magic bytes) → thẻ chữ thương hiệu. Backend **không** thêm biến thể ảnh.
   **Không** dùng file convention `opengraph-image.tsx`: ở route động, Next 16 gắn hậu tố hash đổi theo
   mỗi bản build (`/opengraph-image-b3yc0w`) → JSON-LD không trỏ tới được, URL cũ 404 sau mỗi lần deploy.
2. Meta description: ngắn hơn 150 ký tự → nối "Chuẩn bị X phút, nấu Y phút, Z phần · {Danh mục}."
   (`cookTime = 0` → "không cần nấu", D19), vẫn thiếu thì nối thêm một câu giới thiệu chung; dài hơn 160
   → cắt tại ranh giới từ + "…", tổng ≤ 160. Mô tả cực ngắn có thể vẫn dưới 150 — chấp nhận. Không thêm
   validation độ dài cho tác giả.
3. "Pass 100%" = Google Rich Results Test **0 lỗi**. Cảnh báo thiếu `aggregateRating`/`video` chấp nhận.
4. `/search` → `noindex, follow`; `/auth/login`, `/auth/register` → `noindex`.
5. Canonical luôn là đường dẫn **không query param** (`/recipes?page=2` → `/recipes`), gốc `NEXT_PUBLIC_SITE_URL`.

**Vì:** đạt kích thước OG mà không cần migration/đổi DTO; tác giả không bị ép viết mô tả theo độ dài SEO;
không thể có star rating khi rating ngoài scope; trang kết quả tìm kiếm nội bộ và trang đăng nhập không nên
lên Google (khuyến nghị của Google); query param chỉ dùng cho filter/sort/paging (NFR-SEO-004).

**Sửa SRS:** NFR-SEO-001 — bỏ "star rating" khỏi kết quả mong đợi, ghi "pass = 0 lỗi". NFR-SEO-002 —
ghi rõ cách xử lý description ngoài 150–160 và nguồn og:image.

---

## D62 — FR-AUTH-006/007: route trang hồ sơ và chặn route chưa đăng nhập

> 2026-11-03. Xem `docs/plans/lab08-ke-hoach-chinh-sua-cac-lab.md` (Việc 2).

**Nguồn:** SRS mục 5.1 liệt kê trang `/profile` (CSR, bắt buộc đăng nhập); `permissions.md`,
`team-assignment.md`, `roadmap.md` chép theo. Code thực tế (FR-AUTH-006/007) đặt trang ở
`/dashboard/profile`, nằm trong nhóm `/dashboard/*` mà SRS 5.1 cũng ghi là CSR. Thêm vào đó,
`proxy.ts` chỉ refresh token, chưa có gì chặn người chưa đăng nhập vào `/dashboard/*`.

**Chốt:**

1. Trang hồ sơ ở **`/dashboard/profile`**. Không tạo `/profile` (kể cả redirect).
2. Callback `authorized` của Auth.js (`lib/auth/callbacks.ts`), chạy trong `proxy.ts`: chưa
   đăng nhập hoặc phiên hỏng (`session.error`) vào `/dashboard` hoặc `/dashboard/*` → redirect 307
   `/auth/login?callbackUrl=<pathname+search>`. Chỉ nhận đường dẫn nội bộ (`safeCallbackUrl`).
3. Chỉ kiểm tra "đã đăng nhập chưa". Kiểm tra role (Author/Admin/Owner) là việc của từng trang,
   và backend vẫn là nơi chặn thật (NFR-SEC-006).

**Vì:** giữ mọi trang cần đăng nhập dưới một tiền tố để một quy tắc chặn là đủ; không đổi code đã
chạy và có test (header, dashboard, FR-AUTH-006/007 đều đang trỏ `/dashboard/profile`).

**Sửa SRS:** mục 5.1 — đổi `/profile` thành `/dashboard/profile`.

---

## D63 — NFR-SEC-003: IP để rate limit khi request đi vòng qua Next.js

> 2026-11-05. Phát hiện khi review PR #62 (lab08).

**Nguồn:** D15 đếm "theo IP", D60 lấy IP sau `UseForwardedHeaders`. Nhưng đăng ký, đăng nhập,
Google, refresh token và logout đều do **server Next.js** gọi (Auth.js `authorize`, callback `jwt`
trong `proxy.ts`, `events.signOut`) qua `API_INTERNAL_URL` — không qua Nginx, không có
`X-Forwarded-For`. API thấy mọi người dùng là IP container `frontend`, nên `/auth/*` chỉ còn
10 req/phút **cho cả site**: khoảng 150 người online đã đủ làm refresh bị 429 → phiên hỏng → đăng
xuất hàng loạt. Thêm nữa, `location /api/` của `nginx.prod.conf` có `proxy_set_header` riêng nên
không kế thừa `X-Forwarded-For`/`X-Real-IP` ở mức server — request từ trình duyệt cũng chung IP
của Nginx.

**Chốt:**

1. Nginx: mọi `proxy_set_header` chung đặt ở mức `server`, location `/api/` không khai báo riêng.
2. Lời gọi server → API của Next.js gửi `X-Forwarded-For: <IP người dùng>` (`lib/auth/client-ip.ts`).
   IP lấy từ **`X-Real-IP`** do Nginx ghi đè bằng `$remote_addr` — không đọc `X-Forwarded-For` của
   request tới Next.js vì phần đầu chuỗi do client tự gửi. Ngoài request scope → không gửi header.
3. API giữ cấu hình D60 (tin `X-Forwarded-For` từ mạng nội bộ, `ForwardLimit = 1`). Chỉ Nginx
   publish cổng nên client bên ngoài không gửi thẳng tới API hay frontend được.
4. Development bật rate limit với ngưỡng cao (`appsettings.Development.json`: auth 100, upload 50,
   chung 1000) — ở máy dev mọi request cùng là `127.0.0.1`. Production dùng đúng số D15.
5. Rate limit tự viết bằng `PartitionedRateLimiter` + `RateLimitingMiddleware`, không dùng
   `UseRateLimiter()`: D15 đòi `X-RateLimit-*` trên mọi response, `UseRateLimiter` chỉ cho can
   thiệp lúc từ chối.

**Vì:** giữ nguyên ngưỡng D15 và mục đích chống brute force theo từng người; sửa đúng chỗ làm mất
IP thay vì nới giới hạn cho IP nội bộ (nới thì kẻ tấn công đi qua form đăng nhập là thoát giới hạn).

**Sửa SRS:** NFR-SEC-003 — ghi chú "IP" là IP người dùng cuối, kể cả khi request do server
frontend gọi thay.

---

## D64 — NFR-PERF-005: chỉ số Lighthouse CI đo được

> 2026-11-05. Phát hiện khi lập kế hoạch NFR-PERF-005 (`plans/NFR-PERF-005-lighthouse-ci.md`).

**Nguồn:** SRS §NFR-PERF-005 yêu cầu "đo bằng Lighthouse CI" cả LCP, CLS, **INP** và
"First Load JS Bundle ≤ 200KB (gzipped)". Nhưng Lighthouse chạy chế độ navigation — tải trang,
không có người bấm — nên **không có INP**. Next 16 cũng không còn in cột "First Load JS" khi
`next build`, nên không có con số build nào để đối chiếu.

**Chốt:**

1. **INP → TBT (Total Blocking Time) ≤ 200ms.** TBT là chỉ số lab Google khuyên dùng thay INP
   (cùng đo main thread bị chặn). INP thật chỉ có từ dữ liệu người dùng (CrUX) — ngoài phạm vi v1.
2. **Bundle → `resource-summary:script:size` ≤ 204800 byte** mỗi trang: tổng JS tải qua mạng
   (transfer size, đã nén) mà Lighthouse ghi nhận khi mở trang.
3. **Preset mobile** (mặc định Lighthouse: 4G chậm, CPU ×4) — đúng cách Google đánh giá Core Web
   Vitals. Mỗi URL chạy **5** lần, assert theo trung vị (ban đầu 3 — đổi theo mục 7).
4. **Trang đo:** 6 trang public — `/`, `/recipes`, `/recipes/{slug}`, `/categories`,
   `/categories/{slug}`, `/search?q=`. Không đo `/dashboard/*` (CSR sau đăng nhập) và `/auth/*`.
5. Báo cáo lưu filesystem + artifact CI, **không** đẩy lên `temporary-public-storage` (công khai).
6. **Trang ISR không có `loading.tsx`** (bổ sung 2026-11-05 sau lần chạy CI đầu: `/` LCP 2569ms).
   `loading.tsx` bọc trang trong Suspense; kể cả trang tĩnh, HTML prerender vẫn gửi skeleton trước và
   nội dung thật trong `div hidden`, tráo vào bằng script `$RC` (React gom lần tráo ~300ms) → phần tử LCP
   vẽ sau khi JS chạy. Đã bỏ `app/(home)/loading.tsx`, `app/categories/(list)/loading.tsx`. Đổi lại:
   chuyển trang client sang hai trang này không có skeleton nếu RSC chưa prefetch xong (`<Link>` prefetch
   sẵn route tĩnh nên thường tức thì). NFR-USE-004 cho trang SSR/CSR không bị ảnh hưởng.
7. **5 lần đo mỗi URL thay vì 3** (bổ sung 2026-11-06). PR chỉ sửa tài liệu + script ops (không đụng
   frontend) vẫn đỏ: `/search` TBT **202ms** (> 200) trên runner, trong khi cùng code frontend đã xanh ở
   lần chạy trước — dao động đo, không phải hiệu năng giảm. Trung vị 5 lần ổn định hơn 3; **ngưỡng giữ
   nguyên** theo SRS (không nới). Đổi lại job dài thêm ~3 phút. `/search` vẫn sát ngưỡng TBT — nếu còn
   đỏ với 5 lần thì đó là tín hiệu thật, cần giảm việc của main thread trên trang này.

**Vì:** giữ đúng ý SRS (Core Web Vitals + giới hạn JS) bằng chỉ số Lighthouse thực sự đo được;
không đặt một assert không bao giờ có dữ liệu.

**Sửa SRS:** NFR-PERF-005 — thay "INP ≤ 200ms (Lighthouse CI)" bằng "TBT ≤ 200ms (Lighthouse CI,
lab); INP theo dõi qua CrUX"; ghi rõ cách đo bundle là transfer size JS mỗi trang.

---

## D65 — CONS-009: S3 cho dev/CI khi MinIO khóa phân phối image

> 2026-11-05. Phát hiện khi job Lighthouse CI không pull được MinIO (PR #64). Plan:
> `plans/CONS-009-dev-s3mock.md`.

**Nguồn:** SRS §6.5 và CLAUDE.md mục 2 chọn MinIO làm object storage. Nhưng MinIO Inc. đã khóa
phân phối image công khai: `docker manifest inspect` (2026-11-05) — `quay.io/minio/minio:latest`,
`quay.io/minio/mc:latest` và **cả digest prod** `quay.io/minio/minio@sha256:14cea4…` trả "no such
manifest"; `minio/minio` trên Docker Hub trả "unauthorized". Máy dev clone mới không chạy được
`docker compose up -d`; chỉ máy còn cache image mới chạy.

**Chốt:**

1. **Dev (`docker-compose.yml`) + CI dùng `adobe/s3mock:5.2.3`** — cùng S3 với integration test
   (`IntegrationTests/Common/S3MockContainer.cs`). Service giữ tên `minio`, cổng host 9000, nên
   `Minio:Endpoint` dev và `next.config.ts` không đổi. Bucket tạo bằng biến môi trường (không cần
   `mc`, cũng bị khóa); object lưu volume `s3mockdata`, giữ qua restart. GET object không xác thực —
   khớp ảnh public D16. Không có console web.
2. **Code không đổi.** `MinioFileStorageService` chỉ dùng S3 API chuẩn (AWSSDK.S3, path-style) —
   tên lớp và khóa cấu hình `Minio:*` giữ nguyên.
3. **(Đã đóng bởi D66 — prod dùng RustFS.)** **Prod (`docker-compose.prod.yml`) chưa đổi** — vẫn MinIO pin digest + `MINIO_IMAGE`/`docker load`
   (`docs/deployment.md` mục 3). **Rủi ro mở:** server mới không pull được image; cần quyết định thay
   thế cho prod (ứng viên còn pull tự do: RustFS, SeaweedFS) ở việc riêng. S3Mock **không** dùng cho prod
   (không phân quyền, không thiết kế cho dữ liệu thật).

**Vì:** gỡ chặn cả nhóm ngay với thay đổi nhỏ nhất, dùng lại S3 đã được kiểm trong test và CI;
tách quyết định prod (cần thử kỹ) khỏi việc gỡ chặn dev.

**Sửa SRS:** §6.5 — ghi chú môi trường dev/test dùng S3-compatible mock; MinIO (hoặc thay thế S3
tương thích) cho production.

---

## D66 — CONS-009: S3 production = RustFS (đóng D65.3)

> 2026-11-05. Plan: `plans/CONS-009-prod-rustfs.md`.

**Nguồn:** D65.3 — server mới không dựng được `docker-compose.prod.yml` vì image MinIO (kể cả digest
ghim) đã bị gỡ; cách `docker load` chỉ chạy khi còn máy giữ cache. Kiểm 2026-11-05: nhóm **chưa có**
server prod thật (`SITE_DOMAIN=culinary.localhost`, không có workflow deploy) → không có dữ liệu cần chuyển.

**Chốt:**

1. **Service `minio` của prod chạy `rustfs/rustfs:1.0.3`** (bản 1.0 chính thức, 17/10/2026; ghi đè qua
   `S3_IMAGE`). Giữ tên service + cổng 9000 → `nginx.prod.conf`, `Minio__Endpoint` không đổi; khóa vẫn
   lấy từ `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` → `.env.production` cũ dùng được. Volume mới `s3data`.
2. **`minio-init` = `amazon/aws-cli:2.38.0`** (thay `mc`, cũng bị gỡ): `head-bucket || create-bucket`
   rồi `put-bucket-policy` **chỉ `s3:GetObject` cho `*`** — đúng D16 (ảnh public), chạy lại không lỗi.
3. Chọn RustFS thay SeaweedFS: RustFS thay thẳng MinIO (S3 API, bucket policy chuẩn, `/health`),
   SeaweedFS cấu hình quyền ẩn danh bằng file identity riêng. `adobe/s3mock` chỉ cho dev/test (D65).

**Kiểm chứng:** RustFS + aws-cli độc lập (GET ẩn danh 200, PUT/LIST ẩn danh 403, còn sau restart);
stack prod trên máy: `scripts/verify-prod.sh` bước 1–8 đạt (gồm 2 kiểm tra mới: PUT và LIST ẩn danh qua
Nginx → 403), API `/health` Healthy, job sitemap ghi `seo/sitemap.xml` vào RustFS qua
`MinioFileStorageService` và Nginx đọc lại 200.

**Vì:** server mới dựng được chỉ bằng image công khai; giữ nguyên code và cấu hình Nginx/API.

**Sửa SRS:** §6.5 — object storage production "S3 tương thích (RustFS)" thay "MinIO".

---

## D67 — NFR-SEC-004: giới hạn kích thước upload ở tầng HTTP

> 2026-11-05. Plan: `plans/NFR-SEC-004-upload-csp.md`.

**Nguồn:** NFR-SEC-004 "kiểm tra size **trước khi read stream** (không buffer toàn bộ)". Validator
(`UploadRecipeImageCommandValidator`) kiểm `Length` trước khi đọc — nhưng endpoint nhận `IFormFile`, nên
ASP.NET đã nhận + đệm **toàn bộ** body (mặc định Kestrel tới 30MB) trước khi validator chạy. Nginx chặn 6M
nhưng chỉ đứng trước API (dev chạy thẳng Kestrel). Thêm nữa, body vượt giới hạn ném
`BadHttpRequestException` mà `GlobalExceptionMiddleware` không map → **500**.

**Chốt:**

1. Endpoint `POST /recipes/{id}/images` giới hạn form **6MB** (5MB CONS-007 + dư multipart — khớp
   `client_max_body_size 6M` của Nginx). Đọc form dừng ngay khi vượt, không nhận hết body.
2. Vượt giới hạn → **413** RFC 7807 `type: FILE_SIZE_EXCEEDED`. File 5–6MB vẫn lọt tầng HTTP và bị
   validator trả **400** `FILE_SIZE_EXCEEDED` như cũ (cùng mã, khác status — status báo tầng chặn).
3. `RouteHandlerOptions.ThrowOnBadRequest = true` ở **mọi** môi trường (mặc định chỉ Development: Testing/
   Production trả 400 **rỗng**, Development ném lỗi → 500). `GlobalExceptionMiddleware` map
   `BadHttpRequestException`: lỗi đọc form (`InvalidDataException` bên trong — vượt giới hạn) hoặc status
   413 → **413** `FILE_SIZE_EXCEEDED`; còn lại (JSON hỏng, thiếu tham số) → 400 `VALIDATION_ERROR` — đều
   RFC 7807 thay vì body rỗng/500.
   *Phát hiện lúc code:* `WriteAsJsonAsync(problem)` ghi đè `Content-Type` thành `application/json` →
   **mọi** response lỗi trước đây sai CONS-005. Sửa: truyền `contentType: "application/problem+json"`
   (như `RateLimitingMiddleware`). Client (`res.json()`, axios) parse cả hai nên không vỡ.
4. Là cấu hình transport, **không** phải validation nghiệp vụ → không vi phạm CONS-008; luật 5MB vẫn
   chỉ nằm ở validator.

**Vì:** đúng ý "không buffer toàn bộ" của SRS ngay cả khi request không đi qua Nginx; client nhận lỗi
RFC 7807 có mã thay vì 500.

**Sửa SRS:** FR-RCP-008 A2 / bảng mã lỗi — `FILE_SIZE_EXCEEDED` có thể là 413 khi bị chặn ở tầng HTTP.

---

## D68 — NFR-SEC-004: Content-Security-Policy

> 2026-11-05. Plan: `plans/NFR-SEC-004-upload-csp.md`.

**Nguồn:** NFR-SEC-004 yêu cầu "Content-Security-Policy header" chống XSS. Trước 2026-11-05 không có
CSP ở Nginx, Next.js lẫn API.

**Chốt:**

1. **Trang Next.js** (`frontend/lib/security/csp.ts`, gắn qua `headers()` của `next.config.ts`):
   `default-src 'self'` · `script-src 'self' 'unsafe-inline'` · `style-src 'self' 'unsafe-inline'` ·
   `img-src 'self' data: blob:` + nguồn ảnh cấu hình · `font-src 'self'` · `connect-src 'self'` + origin
   API · `object-src 'none'` · `base-uri 'self'` · `frame-ancestors 'none'` ·
   `form-action 'self' https://accounts.google.com`. Dev thêm `'unsafe-eval'` (React Refresh) và `ws:` (HMR).
2. **`'unsafe-inline'` cho script là đánh đổi có chủ ý:** App Router chèn script inline (payload RSC);
   CSP dùng nonce buộc **mọi trang render động** → mất ISR mà SRS 5.1 yêu cầu (`/` 3600, `/recipes/[slug]`
   300…). Hash không dùng được vì payload RSC khác nhau mỗi trang. Lớp chống XSS chính vẫn là React tự
   escape + JSON-LD escape; CSP vẫn chặn script từ domain lạ, plugin, `<base>` giả, nhúng khung, form gửi ra ngoài.
3. **API** (middleware `SecurityHeadersMiddleware`): `default-src 'none'; frame-ancestors 'none'` — API chỉ
   trả JSON. Gắn cả trên response lỗi.
4. **Đúng một header CSP mỗi response** — Nginx **không** thêm CSP (như bài học HSTS ở D60).

**Vì:** đáp ứng NFR-SEC-004 mà không phá ISR (SRS 5.1) hay NFR-PERF-005.

**Sửa SRS:** NFR-SEC-004 — ghi rõ CSP cho phép `'unsafe-inline'` script do ràng buộc ISR của Next.js
App Router.

---

## D69 — FR-RCP-003/004: hợp đồng request tạo/sửa recipe

> 2026-11-06. Phát hiện lúc code FR-RCP-003.

**Nguồn mâu thuẫn:**

- FR-RCP-003 bước 1 dùng `prepTimeMinutes`/`cookTimeMinutes`; FR-RCP-004 và SRS Chương 8 (bảng endpoint
  `/recipes`) dùng `prepTime`/`cookTime`.
- Chương 8 cho `PUT /recipes/{id}` mọi trường tùy chọn (`title?`, `description?`…), còn FR-RCP-004 bước 1
  là body đầy đủ.
- FR-RCP-003 A2 trả 422 khi `categoryId` không tồn tại (D4 đã bỏ 422 nhưng chưa nói 400 hay 404).
- SRS không nói `difficulty` gửi dạng chữ hay số.

**Chốt:**

1. Tên trường theo Chương 8 và entity: `title, description, categoryId, prepTime, cookTime, servings,
   difficulty, instructions?, nutrition?` (+ `steps?`, `ingredients?` ở POST).
2. `difficulty` nhận **chuỗi hoặc số**: `"Easy"|"Medium"|"Hard"|"Expert"` (không phân biệt hoa thường)
   hoặc `1`–`4` (giống D14 và giống giá trị số mà `RecipeDetailDto` trả về).
3. `categoryId` không tồn tại hoặc đã xóa mềm → **400 `VALIDATION_ERROR`**, lỗi gắn vào trường
   `CategoryId` (đây là lỗi dữ liệu đầu vào, không phải tài nguyên trên URL không tồn tại).
4. `PUT /recipes/{id}` là **thay toàn bộ** các trường cơ bản như FR-RCP-004 (đúng ngữ nghĩa PUT, form sửa
   của frontend luôn gửi đủ). `instructions: null` = xóa; `nutrition: null` hoặc vắng = **giữ nguyên**
   (FR-RCP-004 bước 8 "cập nhật Nutrition nếu có"). PUT không đụng steps/ingredients — dùng FR-RCP-009/010.
5. Steps trong body POST: `{ title, description, timerMinutes?, imageUrl? }` — **không** có `stepNumber`,
   server đánh số 1, 2, 3… theo thứ tự gửi (D6). Ingredients: `{ name, quantity?, unit?, notes? }`,
   `orderIndex` theo thứ tự gửi (D7). Tối đa **100** step và 100 ingredient mỗi request.
6. `imageUrl` của step chỉ nhận URL tuyệt đối `http`/`https` (chặn `javascript:` khi frontend render).
7. Response của POST (201) và PUT (200) là `RecipeDetailDto` đầy đủ, giống `GET /recipes/{slug}`.

**Vì:** Chương 8 là hợp đồng API, khớp tên entity; số và chữ đều nhận được để frontend gửi lại đúng giá trị
vừa đọc. 400 cho category sai đúng tinh thần D4 (dữ liệu không hợp lệ).

**Sửa SRS:** FR-RCP-003 bước 1 — `prepTimeMinutes/cookTimeMinutes` → `prepTime/cookTime`; A2 → 400
`VALIDATION_ERROR`. Chương 8 — PUT `/recipes/{id}` body đầy đủ, bỏ dấu `?` ở các trường bắt buộc.

---

## D70 — Phân quyền Recipe: `RecipeAuthorizationHandler` dùng `ICurrentUser`

> 2026-11-06. Chi tiết: `adr/0005-phan-quyen-recipe-o-application.md`.

**Nguồn mâu thuẫn:** `permissions.md` và FR-RCP-004 bước 5 ghi `IAuthorizationService.AuthorizeAsync(...)`
(ASP.NET Core) trong Application Layer, nhưng Application không reference ASP.NET và `ICurrentUser`
(đã đóng băng) không có `ClaimsPrincipal`.

**Chốt:** `Application/Recipes/Authorization/RecipeAuthorizationHandler` — `EnsureCanCreate()` (Author/Admin),
`EnsureCanModify(recipe)` (owner hoặc Admin). Sai quyền → 403 `RECIPE_FORBIDDEN`; chưa đăng nhập → 401.
Gọi trong **mọi** command của FR-RCP-003/004/005/006/007/009/010, trước khi ghi.

**Vì:** NFR-SEC-006 chỉ đòi kiểm tra ở Application Layer, không đòi đúng API của ASP.NET. Cùng cách với D50.

**Sửa SRS:** FR-RCP-004 bước 5 — ghi "kiểm tra quyền sở hữu ở Application Layer (owner hoặc Admin)", bỏ tên
API cụ thể. Sửa `permissions.md` mục 1 cho khớp.

---

## D71 — FR-RCP-004: `If-Match` / `RowVersion` / `ETag`

> 2026-11-06. Bổ sung cho D34.

**Nguồn thiếu:** FR-RCP-004 bắt client gửi `RowVersion` trong `If-Match` (hoặc body), nhưng không API nào
trả `RowVersion` cho client; cũng không nói thiếu `If-Match` thì sao.

**Chốt:**

1. `RecipeDetailDto` thêm trường **`rowVersion`** (base64) — có ở `GET /recipes/{slug}`, POST, PUT. POST và PUT
   còn trả header **`ETag: "<base64>"`**.
2. `PUT /recipes/{id}` **bắt buộc** header `If-Match` (chấp nhận có/không ngoặc kép, bỏ tiền tố `W/`).
   Thiếu hoặc không phải base64 → **400 `VALIDATION_ERROR`** (trường `RowVersion`). Không dùng 428 (không
   nằm trong danh sách status D4). Không đọc RowVersion từ body — một nguồn duy nhất.
3. Khác `RowVersion` trong DB → **409 `RECIPE_CONCURRENCY_CONFLICT`** (D4, D34).
4. Thêm/sửa/xóa step, ingredient, ảnh **không** đổi `RowVersion` của recipe — sửa step trong wizard không làm
   form thông tin chung bị 409 oan.

**Vì:** không có giá trị để gửi thì FR-RCP-004 không dùng được. Bắt buộc `If-Match` để không ai vô tình ghi
đè bản mới hơn (mục đích của FR-RCP-004 A2).

**Sửa SRS:** FR-RCP-004 — tiền đề 3 bỏ "hoặc trong request body", thêm "thiếu If-Match → 400"; Chương 8 —
`RecipeDetailDto` có `rowVersion`.

---

## D72 — Slug recipe: chi tiết sinh slug

> 2026-11-06. Bổ sung cho D10, phát hiện lúc code FR-RCP-003.

**Nguồn thiếu:** `SlugHelper` dùng chung (B) chưa gộp `-` liên tiếp, chưa cắt 200 ký tự, chưa xử lý slug cấm
như D10 yêu cầu; `GenerateUniqueAsync` bắt đầu hậu tố từ `-1` (D10 là `-2`). D10 không nói title không còn ký
tự hợp lệ nào (vd. `"!!!!!"`) thì sao. `ExistsBySlugAsync` của recipe bỏ qua bản ghi đã xóa mềm, trong khi
unique index phủ cả chúng (D1) → tạo trùng slug với recipe đã xóa sẽ lỗi 500.

**Chốt:** `CreateRecipeCommandHandler` tự hoàn thiện các bước sau `SlugHelper.Generate` (không sửa file của B):

1. Gộp `-` liên tiếp, cắt `-` đầu/cuối, cắt còn 200 ký tự.
2. Rỗng → `cong-thuc`.
3. Rơi vào `search`/`new`/`edit` → thêm `-1` (D10).
4. Trùng (tính **cả recipe đã xóa mềm**) → `-2`, `-3`… `IRecipeRepository.ExistsBySlugAsync` đổi sang
   `IgnoreQueryFilters()`.
5. Slug không đổi khi sửa title (D13).

**Vì:** đúng D10, D1; không sửa hành vi slug của Category đang chạy.

**Sửa SRS:** không cần (đã nằm trong D10). Gợi ý cho B: đưa bước 1–3 vào `SlugHelper` để Category dùng chung.

---

## D73 — FR-RCP-010: xóa bước, đánh số lại, lỗi và tranh chấp

> 2026-11-06. Phát hiện lúc code FR-RCP-010.

**Nguồn mâu thuẫn / thiếu:**

- `BaseEntity` và D1 nói "không bao giờ xóa vật lý", nhưng unique index `(RecipeId, StepNumber)` (SRS 7.3)
  phủ cả dòng đã xóa mềm: bước đã xóa vẫn giữ số của nó, renumber các bước còn lại sẽ đụng index.
- Unique index đó **không deferrable**: đổi chỗ hai bước hoặc dồn số trong một lần `SaveChanges` vi phạm
  index giữa chừng (Postgres kiểm tra từng câu UPDATE).
- Hai request thêm bước song song cùng tính `Max + 1` → trùng số → lỗi DB 500.
- SRS không có mã lỗi cho bước không tồn tại, không nói `stepNumber` ở PUT vượt số bước thì sao, không nói
  xóa bước cuối của recipe đã Published thì sao.

**Chốt:**

1. **Xóa bước = hard delete** (giống ảnh — D43). D1 chỉ áp cho Recipe: recipe xóa mềm vẫn giữ nguyên bước.
2. **Lưu 2 pha** trong `RecipeRepository.SaveStepsAsync`: pha 1 đẩy các bước bị đổi số ra vùng tạm
  (≥ 1 000 000, thỏa CHECK `StepNumber > 0`) và áp dụng xóa; pha 2 gán số cuối. Cả hai pha nằm trong một
  transaction (`IUnitOfWork.ExecuteInTransactionAsync` có execution strategy).
3. **Khóa dòng recipe** (`SELECT … FOR UPDATE`, tham số hóa) trước khi đọc, cho cả thêm/sửa/xóa bước — các
  lệnh trên cùng recipe chạy tuần tự.
4. Mã lỗi mới **`RECIPE_STEP_NOT_FOUND` (404)** — bước không tồn tại hoặc thuộc recipe khác.
5. `PUT` với `stepNumber` ngoài `1..số bước` → **400 `VALIDATION_ERROR`** (không tự kẹp về biên).
   `stepNumber` vắng = giữ vị trí. `timerMinutes`/`imageUrl` vắng = xóa (PUT thay nội dung bước).
6. `POST` bỏ qua `stepNumber` nếu client lỡ gửi (D6). Response: POST → `RecipeStepDto` (201);
   PUT → **toàn bộ bước** theo thứ tự mới (200), vì đổi vị trí làm đổi số của các bước khác; DELETE → 204.
7. Xóa bước cuối cùng của recipe **Published** → **400 `RECIPE_PUBLISH_INCOMPLETE`** (giữ điều kiện D3 sau
  khi publish). Draft được xóa hết bước.
8. Sửa bước không đổi `RowVersion` / `UpdatedAt` của recipe (D71).

**Vì:** giữ đúng ràng buộc dữ liệu SRS 7.3 (StepNumber liên tục, unique) mà không đổi schema; không request
nào kết thúc bằng 500.

**Sửa SRS:** FR-RCP-010 — ghi rõ xóa bước là xóa vật lý; thêm A3 "bước không tồn tại → 404
`RECIPE_STEP_NOT_FOUND`", A4 "stepNumber ngoài khoảng → 400", A5 "xóa bước cuối của recipe Published → 400";
bỏ 422. Phụ lục B — thêm `RECIPE_STEP_NOT_FOUND`.

---

## D74 — Frontend: gắn Bearer token vào `apiClient` từ phiên Auth.js

> 2026-11-06. Phát hiện lúc làm FE cho FR-RCP-003/004/010.

**Nguồn thiếu:** `frontend/lib/api-client.ts` chỉ có `TODO(S2 — A): … gắn Authorization: Bearer từ
session Auth.js`, chưa có interceptor nào. Mọi lời gọi API cần đăng nhập từ trình duyệt (`/dashboard/*`:
hồ sơ, quản lý danh mục, ảnh, công thức) đi thiếu token → 401. E2E cũ không phát hiện vì chỉ kiểm tra heading.

**Chốt:**

1. Interceptor request của `apiClient` (chỉ chạy trên trình duyệt) gọi `getSession()` của `next-auth/react`
   và gắn `Authorization: Bearer <session.accessToken>`. `/api/auth/session` chạy callback jwt nên access token
   sắp hết hạn được refresh tự động (FR-AUTH-004) — không cần tự xử lý 401.
2. Cache token 30 giây, gộp các lời gọi song song (một trang dashboard bắn nhiều request cùng lúc).
3. Phiên có `error = RefreshTokenError` → không gắn token cũ (SessionExpiryWatcher sẽ đăng xuất).
4. Request đã tự đặt `Authorization` (vd. logout phía server) → giữ nguyên. Phía server (`authorize()`)
   interceptor bỏ qua.

**Vì:** token chỉ nằm trong phiên Auth.js (cookie httpOnly mã hóa, NFR-SEC-002); `getSession()` là cách
duy nhất để JS trình duyệt lấy access token mà không lưu ra localStorage.

**Sửa SRS:** không cần.

---

## D75 — `GET /api/v1/recipes/{id:guid}` cho trang sửa công thức

> 2026-11-06. Phát hiện lúc làm FE cho FR-RCP-004.

**Nguồn thiếu:** route trang sửa là `/dashboard/recipes/[id]/edit` (`permissions.md` mục 4) và mọi lệnh ghi
dùng `{id}`, nhưng API chỉ có `GET /recipes/{slug}` (FR-RCP-002) — trang sửa không đọc được công thức theo id.
`GET /recipes/{slug}` còn qua cache Redis 5 phút; trang sửa cần `rowVersion` mới nhất (D71).

**Chốt:**

1. Thêm `GET /api/v1/recipes/{id:guid}` → `GetRecipeByIdQuery` → `RecipeDetailDto` (có `rowVersion`).
   **Không cache.** Quyền xem giống FR-RCP-002: Published ai cũng xem; Draft/Archived chỉ owner hoặc Admin,
   còn lại 403 `RECIPE_FORBIDDEN`; không có / đã xóa mềm → 404 `RECIPE_NOT_FOUND`.
2. Ràng buộc `:guid` làm route này được ưu tiên hơn `/{slug}`. Để không recipe nào bị "nuốt" mất, slug có
   dạng GUID (title là một chuỗi GUID) được thêm `-1` như slug cấm (bổ sung D72).

**Vì:** cần cho FR-RCP-004 (FE) và chống 409 oan do đọc rowVersion cũ từ cache.

**Sửa SRS:** Chương 8 mục 8.3 — thêm dòng `GET /recipes/{id}`.

---

## D76 — FR-RCP-009: vị trí, xóa, lỗi và tranh chấp của nguyên liệu

> 2026-11-06. Phát hiện lúc code FR-RCP-009. Đi song song với D73 (bước).

**Nguồn mâu thuẫn / thiếu:**

- FR-RCP-009 không nói xóa nguyên liệu là xóa mềm hay xóa vật lý (`BaseEntity` + D1 nói "không xóa vật lý",
  D1 chỉ áp cho Recipe).
- Body POST có `sortOrder?` (D7 đổi thành `orderIndex`, "mặc định 0"), nhưng không nói chèn vào giữa thì các
  nguyên liệu khác ra sao, cũng không nói PUT có đổi vị trí được không.
- SRS không có mã lỗi cho nguyên liệu không tồn tại; không nói xóa nguyên liệu cuối của recipe Published thì sao.
- Hai request thêm song song cùng tính `Max + 1` → trùng `OrderIndex` (không có unique index nên không lỗi DB,
  nhưng thứ tự hiển thị không xác định).

**Chốt:**

1. **Xóa nguyên liệu = hard delete** (giống bước — D73, ảnh — D43). Recipe xóa mềm vẫn giữ nguyên nguyên liệu (D1).
2. `orderIndex` là **vị trí 0-based** trong danh sách. Server luôn giữ `OrderIndex` liên tục `0..n-1` sau mỗi
   lệnh chèn / đổi vị trí / xóa.
   - `POST` — `orderIndex` vắng = xếp cuối; có giá trị `0..n` = chèn vào đó, các nguyên liệu sau dồn xuống.
   - `PUT` — `orderIndex` vắng = giữ vị trí; có giá trị `0..n-1` = chuyển tới đó.
   - Ngoài khoảng → **400 `VALIDATION_ERROR`** (âm: validator; vượt cận trên: domain). Không tự kẹp về biên.
3. `PUT` thay toàn bộ nội dung: `name` bắt buộc; `quantity`/`unit`/`notes` vắng hoặc `null` = **xóa** (giống
   `timerMinutes`/`imageUrl` ở D73). Chuỗi chỉ có khoảng trắng → `null`.
4. Mã lỗi mới **`RECIPE_INGREDIENT_NOT_FOUND` (404)** — nguyên liệu không tồn tại hoặc thuộc recipe khác.
   Thứ tự kiểm tra: 404 recipe → 403 → 404 nguyên liệu.
5. Response: `POST` → `RecipeIngredientDto` (201); `PUT` → **toàn bộ nguyên liệu** theo thứ tự mới (200), vì
   đổi vị trí làm đổi `orderIndex` của nguyên liệu khác; `DELETE` → 204.
6. Xóa nguyên liệu cuối cùng của recipe **Published** → **400 `RECIPE_PUBLISH_INCOMPLETE`** (giữ điều kiện D3).
   Draft được xóa hết.
7. **Khóa dòng recipe** (`GetByIdWithDetailsForUpdateAsync`) cho cả thêm/sửa/xóa — các lệnh trên cùng recipe chạy
   tuần tự. **Không cần lưu 2 pha** (không có unique index trên `OrderIndex`), một lần `SaveChanges`.
8. Sửa nguyên liệu không đổi `RowVersion` / `UpdatedAt` của recipe (D71).

**Vì:** cùng hành vi với bước (D73) để frontend dùng chung một kiểu trình sửa danh sách; không request nào ra 500.

**Sửa SRS:** FR-RCP-009 — `sortOrder` → `orderIndex` (0-based, vắng = cuối); ghi rõ xóa là xóa vật lý; A1 tách
mã `RECIPE_INGREDIENT_NOT_FOUND`; A3 → 400; thêm "xóa nguyên liệu cuối của recipe Published → 400";
PUT trả toàn bộ danh sách. Phụ lục B — thêm `RECIPE_INGREDIENT_NOT_FOUND`.

---

## D77 — FR-RCP-005: chuyển trạng thái publish / unpublish

> 2026-11-06. Phát hiện lúc code FR-RCP-005. Bổ sung cho D3, D39, D71.

**Nguồn mâu thuẫn / thiếu:**

- FR-RCP-005 chỉ nói Draft ↔ Published, không nói recipe **Archived** gọi publish/unpublish thì sao (FR-RCP-006
  có "Unarchive" trong tên nhưng không có endpoint).
- A2 "đã ở trạng thái mong muốn → 200 idempotent" nhưng không nói có ghi DB / đổi `PublishedAt` / đếm metric không.
- `Recipe.Publish()` (Sprint 0) gán `PublishedAt = now` mỗi lần — publish lại sau khi unpublish làm "ngày đăng"
  (JSON-LD `datePublished`, NFR-SEO-001) nhảy lên như bài mới.
- Không nói có cần `If-Match` không (D71 chỉ chốt cho PUT), response "RecipeDto" là DTO nào.
- Lệnh publish chạy song song với lệnh xóa bước/nguyên liệu cuối cùng (D73/D76) có thể để lọt recipe Published
  không có bước/nguyên liệu.

**Chốt:**

1. Domain có hai method: `Publish()` (Draft/**Archived** → Published, kiểm D3) và `Unpublish()` (Published/**Archived**
   → Draft). Cả hai trả `bool` = trạng thái có thực sự đổi không. `MoveToDraft()` cũ giữ nguyên cho dữ liệu test.
   Dùng một command `PublishRecipeCommand(RecipeId, IsPublish, IfMatch?)` như SRS bước 2.
2. **Idempotent:** đã ở trạng thái đích → 200, **không** `SaveChanges` (RowVersion, `UpdatedAt` không đổi),
   **không** đếm metric. Publish recipe đã Published không kiểm lại D3.
3. **`PublishedAt` = thời điểm xuất bản lần đầu**: chỉ gán khi đang `null`; unpublish không xóa, publish lại không đổi.
   "Lần sửa gần nhất" đã có `UpdatedAt`.
4. **`If-Match` tùy chọn**: vắng → bỏ qua kiểm tra (đổi trạng thái không ghi đè nội dung người khác vừa sửa);
   có nhưng sai định dạng → 400 `VALIDATION_ERROR` (trường `RowVersion`); có và lệch → **409
   `RECIPE_CONCURRENCY_CONFLICT`**. Thứ tự: 404 → 403 → 409 → 400 D3.
5. Response: **`RecipeDetailDto`** đầy đủ (giống PUT) + header **`ETag`** với `rowVersion` mới (đổi Status làm đổi
   RowVersion).
6. Handler **khóa dòng recipe** (`GetByIdWithDetailsForUpdateAsync`) trong transaction — publish xếp hàng với lệnh
   sửa bước/nguyên liệu, và quyền được kiểm tra trên bản ghi đã khóa ngay trước khi ghi (`permissions.md`:
   "double-check userId ngay trước khi commit").
7. `RecipeMetrics.RecordPublished()` gọi **sau** transaction thành công, chỉ khi trạng thái thực sự đổi sang Published
   (D39). Log `Information` "Recipe {RecipeId} chuyển sang {Status} bởi {UserId}" làm audit trail (NFR-SEC-006).
8. Cache: xóa tag `recipes` + `recipe:{slug}` (D8) — danh sách, chi tiết, tìm kiếm thấy ngay.

**Vì:** cho Author một đường thoát khỏi Archived mà không cần endpoint mới; giữ ngày đăng ổn định cho SEO; không
request nào tạo ra recipe Published vi phạm D3.

**Sửa SRS:** FR-RCP-005 — mô tả thêm Archived → Published/Draft; A1 → 400 `RECIPE_PUBLISH_INCOMPLETE` (D3, D4);
bước 6 "PublishedAt chỉ gán lần đầu"; bước 8 → `RecipeDetailDto` + `ETag`; thêm "If-Match tùy chọn, lệch → 409".

---

## D78 — FR-RCP-007: chi tiết xóa mềm recipe

> 2026-11-06. Phát hiện lúc code FR-RCP-007. Bổ sung cho D1.

**Nguồn thiếu:** D1 đã chốt soft delete nhưng chưa nói: xóa lại recipe đã xóa trả gì; recipe Published có phải
unpublish trước không; có cần `If-Match` không; lệnh xóa chạy song song với lệnh sửa bước/nguyên liệu/publish thì sao.

**Chốt:**

1. `DELETE /recipes/{id}` gọi `IRecipeRepository.Delete` (`SoftDelete()` → `IsDeleted = true`, `UpdatedAt = now`).
   Steps/ingredients/images **không bị đụng tới** (không đổi `IsDeleted`, không ghi lại dòng), **không** enqueue job xóa
   file MinIO, slug giữ chỗ (D1, D72). Không có metric "deleted".
2. Recipe ở **mọi trạng thái** (kể cả Published) xóa thẳng được — không bắt unpublish trước.
3. Sau khi xóa, mọi thao tác trên recipe đó → **404 `RECIPE_NOT_FOUND`**: `GET /recipes/{slug}`, `GET /recipes/{id}`,
   PUT, publish/unpublish, bước, nguyên liệu, ảnh, **kể cả `DELETE` lần hai** (không idempotent 204 — giữ đúng A1
   của SRS, và client biết recipe không còn).
4. **`If-Match` tùy chọn** giống D77: vắng → bỏ qua; sai định dạng → 400 `VALIDATION_ERROR`; lệch → 409
   `RECIPE_CONCURRENCY_CONFLICT`. Thứ tự: 404 → 403 → 409.
5. Handler **khóa dòng recipe** (`GetByIdWithDetailsForUpdateAsync`) trong transaction: xóa xếp hàng sau lệnh sửa
   bước/nguyên liệu/publish đang chạy (lệnh đến sau thấy recipe đã xóa → 404), quyền kiểm tra trên bản ghi đã khóa
   ngay trước khi ghi (`permissions.md`). Log `Information` làm audit trail (NFR-SEC-006).
6. Response **204**, xóa tag `recipes` + `recipe:{slug}` (D8). Recipe đã xóa không còn tính vào điều kiện
   "category còn recipe" (D2) — xóa recipe cuối cùng thì Admin xóa được category.

**Vì:** đúng D1 (khôi phục được bằng DB), không request nào ghi vào recipe đã xóa, không 500 khi chạy song song.

**Sửa SRS:** FR-RCP-007 — như D1 (soft delete, bỏ cascade và bước 3–5 về MinIO/Hangfire); thêm "If-Match tùy chọn,
lệch → 409"; A1 ghi rõ "kể cả recipe đã xóa".

---

## D79 — FR-RCP-006: lưu trữ (archive) recipe

> 2026-11-07. Phát hiện lúc Explore FR-RCP-006. Bổ sung cho D71, D77, D78.

**Nguồn mâu thuẫn / thiếu:**

- FR-RCP-006 không nói recipe ở trạng thái nào được archive, cũng không nói gọi lại khi đã Archived thì sao.
- Chỉ ghi "HTTP 200 OK", không nói body là gì; không nhắc `If-Match` / concurrency.
- Tên FR có "Unarchive" nhưng không có endpoint (đã chốt ở D77: dùng publish/unpublish).
- `Recipe.Archive()` (Sprint 0) trả `void`, ghi đè `UpdatedAt` mỗi lần gọi — lệch với `Publish()`/`Unpublish()`.

**Chốt:**

1. Archive được từ **Draft và Published**. Thoát khỏi Archived bằng publish/unpublish (D77).
2. **Idempotent:** `Recipe.Archive()` trả `bool`; đã Archived → 200, **không** `SaveChanges` (RowVersion, `UpdatedAt`
   giữ nguyên).
3. **`If-Match` tùy chọn** giống D77/D78: vắng → bỏ qua; sai định dạng → 400 `VALIDATION_ERROR` (trường `RowVersion`);
   lệch → 409 `RECIPE_CONCURRENCY_CONFLICT`. Thứ tự: 404 → 403 → 409.
4. Response **`RecipeDetailDto`** + header **`ETag`** (giống publish).
5. Command riêng `ArchiveRecipeCommand` (CONS-002). Handler **khóa dòng recipe** trong transaction; log `Information`
   "Recipe {RecipeId} chuyển sang Archived bởi {UserId}" làm audit trail (NFR-SEC-006).
6. **`PublishedAt` giữ nguyên** khi archive — publish lại không đổi ngày đăng (D77.3).
7. **Không thêm metric** — D39 chỉ đếm publish. Cache: xóa tag `recipes` + `recipe:{slug}` (D8).

**Vì:** cùng hành vi với publish/unpublish/delete, client dùng chung một cách xử lý; gọi lặp không làm hỏng `ETag`
của tab khác.

**Sửa SRS:** FR-RCP-006 — tiền điều kiện "Status là Draft hoặc Published"; thêm A3 "đã Archived → 200 idempotent";
thêm "If-Match tùy chọn, lệch → 409"; kết quả trả `RecipeDetailDto` + `ETag`; bỏ "Unarchive" khỏi tên (xem D77).

---

## D80 — Xóa cache Next.js sau khi đổi trạng thái recipe

> 2026-11-07. Phát hiện bằng E2E lúc làm frontend FR-RCP-006. Bổ sung cho D8, D77, D78, D79 và SRS 5.1.

**Nguồn mâu thuẫn / thiếu:**

- SRS 5.1 bắt trang công khai render ISR (`/` 3600, `/recipes/[slug]` 300, `/categories/[slug]` 600). D8 chỉ nói
  xóa cache **Redis** ở backend; không ai xóa data cache của Next.js.
- Hệ quả: recipe vừa hủy xuất bản / lưu trữ / xóa vẫn hiện cho Guest tới 5 phút (chi tiết), 10 phút (danh mục),
  1 giờ (trang chủ) — trái "kết quả mong đợi" của FR-RCP-005/006/007 ("không còn xuất hiện trong public listing")
  và để lộ nội dung tác giả đã muốn ẩn. E2E `recipe-editor.spec.ts` bắt được: archive xong `/recipes/{slug}` vẫn 200.
- Fetch trang chủ và trang danh mục không gắn tag nên không thể xóa theo tag.

**Chốt:**

1. Server Action `revalidateRecipeAction(slug)` (`frontend/app/dashboard/recipes/actions.ts`): chỉ chạy khi có phiên
   Auth.js; gọi `updateTag('recipes')` và `updateTag('recipe:{slug}')` (slug phải đúng dạng D72, sai thì bỏ qua tag
   thứ hai). Dùng `updateTag` (hết hạn ngay) thay `revalidateTag(tag, 'max')` (stale-while-revalidate — người đầu
   tiên vẫn thấy bản cũ).
2. Gọi **sau khi API thành công** của publish, unpublish, archive, delete — trước khi báo toast. Lỗi của action
   **không** làm hỏng thao tác (API đã ghi xong): nuốt lỗi, cache tự hết hạn theo ISR.
3. Gắn tag `recipes` cho fetch danh sách ở trang chủ (`lib/recipes.ts`) và chi tiết danh mục (`lib/categories.ts`).
4. **Không** gọi sau các lệnh sửa nội dung (FR-RCP-004/008/009/010): nội dung đổi chậm tối đa một chu kỳ ISR là chấp
   nhận được theo SRS 5.1; chỉ thay đổi **quyền xem** mới phải có hiệu lực ngay.
5. Không đụng backend: backend không biết Next.js (CONS-001 tinh thần tách lớp), không cần webhook hay secret mới.

**Vì:** nội dung tác giả đã ẩn phải biến mất ngay; giữ được ISR cho hiệu năng (NFR-PERF-005).

**Sửa SRS:** mục 5.1 — thêm "trang ISR bị xóa cache theo tag khi recipe đổi trạng thái hiển thị".

---

## D81 — Danh sách "công thức của tôi" cho `/dashboard/recipes`

> 2026-11-07. Phát hiện lúc Explore trang `/dashboard/recipes`. Bổ sung cho FR-RCP-001, D8, D14.

**Nguồn mâu thuẫn / thiếu:**

- SRS 5.1 có route `/dashboard/recipes` — "Quản lý danh sách recipe của user" (CSR, bắt buộc đăng nhập) — nhưng bảng
  8.3 không có endpoint nào trả recipe của riêng người dùng.
- FR-RCP-001 trả Author tập "Published của mọi người + Draft/Archived của mình": trộn bài người khác, không dùng làm
  trang quản lý được; cũng không có cách lọc theo trạng thái.

**Chốt:**

1. Thêm tham số **`mine=true`** vào `GET /api/v1/recipes`: chỉ recipe có `AuthorId == currentUserId`, mọi trạng thái
   (trừ đã xóa mềm — D1). **Không** tạo route `/recipes/mine`: đụng `/recipes/{slug}` (recipe tên "Mine" có slug `mine`).
2. `mine=true` khi chưa đăng nhập → **401** `AUTH_TOKEN_INVALID` (endpoint vẫn `AllowAnonymous` cho Guest xem danh sách).
3. Thêm bộ lọc **`status`** (`Draft|Published|Archived`, không phân biệt hoa thường), áp **sau** bộ lọc quyền của
   FR-RCP-001 — Guest gửi `status=Draft` chỉ nhận danh sách rỗng, không lộ gì. Giá trị lạ → 400 `VALIDATION_ERROR` (D4).
4. Cache Redis (D8): key `recipes:list:{hash}` thêm `mine` + `status`; key vốn đã tách theo người xem, tag `recipes`
   vẫn bị xóa sau mọi lệnh ghi recipe — danh sách thấy thay đổi ngay.
5. **Admin** vào `/dashboard/recipes` cũng chỉ thấy bài của mình (SRS: "của user"). Màn quản lý toàn bộ recipe cho Admin
   ngoài scope v1.
6. `RecipeSummaryDto` giữ nguyên (đã có `status`, `createdAt`).

**Vì:** dùng lại toàn bộ phân trang/sắp xếp/validator/cache của FR-RCP-001, không thêm route dễ đụng slug, không lộ dữ
liệu vì bộ lọc quyền luôn chạy trước.

**Sửa SRS:** FR-RCP-001 — thêm tham số `mine` (cần đăng nhập, 401) và `status`; bảng 8.3 dòng `GET /recipes` thêm
`&mine&status`.

---

## D82 — Trang tổng quan Author cho `/dashboard`

> 2026-11-08. Phát hiện lúc Explore trang `/dashboard`. Bổ sung cho SRS 5.1, FR-RCP-001, D81.

**Nguồn mâu thuẫn / thiếu:**

- SRS 5.1 có route `/dashboard` — "Trang tổng quan của Author/Admin" (CSR, bắt buộc đăng nhập) — nhưng không nói trang
  hiện gì, và bảng 8.3 không có endpoint thống kê nào.

**Chốt:**

1. Với tài khoản có role Author hoặc Admin, `/dashboard` hiện phần **tổng quan công thức của tôi**:
   - 4 ô số: **Tổng · Nháp · Đã xuất bản · Đã lưu trữ**, mỗi ô là link sang `/dashboard/recipes` (Tổng) hoặc
     `/dashboard/recipes?status=draft|published|archived`;
   - **5 công thức mới nhất** (`-createdAt`) kèm badge trạng thái và link sửa; chưa có bài nào → ô trống kèm lời mời
     tạo công thức.
   Các lối tắt có sẵn giữ nguyên.
2. **Không thêm endpoint.** Frontend gọi song song 4 lần `GET /api/v1/recipes?mine=true` (D81): một lần `pageSize=5`
   lấy tổng + 5 bài mới nhất, ba lần `status=Draft|Published|Archived&pageSize=1` chỉ đọc `totalCount`.
3. Phạm vi số liệu theo D81: chỉ bài của người đang đăng nhập, kể cả Admin; bài đã xóa mềm không tính (D1).
4. Tài khoản không có role Author/Admin không thấy phần này và **không gọi API**.
5. Một trong 4 lời gọi lỗi → chỉ phần tổng quan báo lỗi + nút "Thử lại"; lời chào và lối tắt vẫn dùng được.
6. Số liệu mới ngay sau lệnh ghi: Redis xóa tag `recipes` sau mọi lệnh ghi recipe (D8); query TanStack nằm dưới tiền tố
   `['dashboard', 'my-recipes']` nên bị invalidate cùng danh sách sau publish/unpublish/archive/delete.
   Query này đặt `staleTime: 0` — luôn tải lại khi mở `/dashboard` (wizard tạo bài không invalidate danh sách).

**Vì:** dùng lại toàn bộ phân quyền, validator, cache của FR-RCP-001/D81; không đụng backend, không lệch bảng 8.3. Bốn
lời gọi đều rất nhẹ (`pageSize` 1–5) và có cache Redis 15 phút theo từng người xem.

**Sửa SRS:** mục 5.1 dòng `/dashboard` — "Trang tổng quan: số công thức của tôi theo trạng thái + 5 bài mới nhất + lối tắt".

---

## D83 — Cách đo Accessibility (NFR-USE-002)

> 2026-11-08. Phát hiện lúc Explore NFR-USE-002. Bổ sung cho NFR-USE-002, NFR-MAINT-002.

**Nguồn mâu thuẫn / thiếu:**

- SRS NFR-USE-002 bắt thử screen reader với **NVDA (Windows) và VoiceOver (macOS/iOS)**. Cả nhóm 4 người chỉ có máy
  Windows — không thử được VoiceOver.
- SRS không nói "đạt WCAG 2.1 AA" được chứng minh bằng cách nào (traceability ghi "axe + NVDA/VoiceOver").

**Chốt:**

1. Screen reader v1 **chỉ thử NVDA** trên Windows. VoiceOver ghi "chưa kiểm" trong `traceability.md` — không coi là đạt.
2. Kiểm tra tự động: `frontend/e2e/a11y.spec.ts` chạy axe-core (`@axe-core/playwright`) với tag `wcag2a`, `wcag2aa`,
   `wcag21a`, `wcag21aa` trên mọi route của SRS 5.1 (kể cả trang cần đăng nhập) — **0 vi phạm** mới đạt. Không tắt
   rule nào; ngoại lệ phải ghi D mới.
3. Kiểm tra tay (axe không phát hiện được): `docs/a11y-checklist.md` — điều hướng bàn phím từng luồng chính, NVDA đọc
   form/lỗi/toast, tương phản thành phần UI ≥ 3:1 (WCAG 1.4.11, ví dụ viền ô nhập).
4. NFR-USE-002 chỉ ✅ khi (2) xanh **và** (3) đã chạy hết, ghi ngày + người chạy.

**Vì:** không có thiết bị Apple thì ghi rõ phạm vi còn hơn đánh dấu đạt mà không thử; axe trong E2E chặn hồi quy tương
phản/ARIA cho các thay đổi sau.

**Sửa SRS:** NFR-USE-002 — "Screen reader: test với NVDA (Windows). VoiceOver ngoài phạm vi v1."

---

## D84 — Responsive: breakpoint, "touch-friendly", cách đo (NFR-USE-001)

> 2026-11-08. Phát hiện lúc Explore NFR-USE-001. Bổ sung cho NFR-USE-001.

**Nguồn mâu thuẫn / thiếu:**

- SRS: Tablet 768–1199px là **lưới 2 cột**, Desktop ≥ 1200px. Tailwind mặc định `lg` = 1024px — lưới recipe đang
  hiện 3 cột từ 1024px, lệch SRS.
- "Touch-friendly" không có con số.
- SRS bắt thử bằng BrowserStack (iOS, Android) — nhóm không có tài khoản.
- Bảng quản lý danh mục (Admin) tràn ngang ở 320px; SRS không nói bảng dữ liệu xử lý thế nào trên mobile.

**Chốt:**

1. Breakpoint theo SRS: `md` = 768px (mặc định Tailwind), **`lg` = 1200px** (`--breakpoint-lg: 75rem` trong
   `frontend/app/globals.css`). Dưới 768px: 1 cột; 768–1199px: tối đa 2 cột cho lưới card; từ 1200px: đầy đủ.
2. Touch-friendly = mọi vùng bấm **≥ 24×24px** (WCAG 2.5.8). Link nằm trong đoạn văn được miễn. 44×44px là khuyến
   nghị cho nút chính, không bắt buộc.
3. Bảng dữ liệu trên mobile được cuộn ngang **trong khung của bảng** (WCAG 1.4.10 cho phép với bảng); cả trang không
   bao giờ cuộn ngang. Cột phụ (vd. Slug) được ẩn dưới 640px.
4. Không có BrowserStack: đo bằng Playwright giả lập (320/375/768/1024/1200/1440, bật cảm ứng) —
   `frontend/e2e/responsive.spec.ts`. Thiết bị iOS/Android thật ghi "chưa kiểm" trong `traceability.md`.

**Vì:** bám đúng con số SRS cho bố cục; dùng tiêu chí WCAG có sẵn thay cho chữ "touch-friendly" mơ hồ; không có thiết
bị thì ghi rõ phạm vi (giống D83).

**Sửa SRS:** NFR-USE-001 — ghi "touch-friendly: vùng bấm ≥ 24×24px"; "Kiểm thử: Chrome DevTools / Playwright giả lập;
BrowserStack ngoài phạm vi v1".

---

## D85 — FR-RCP-003: hai request tạo recipe cùng slug đến cùng lúc

> 2026-11-09. Phát hiện khi chạy E2E song song (`POST /recipes → 500`). Bổ sung cho D10, D72.

**Nguồn thiếu:** D10 chốt "slug trùng → auto-suffix `-2`, `-3`…, không bao giờ 409", D72 chốt cách tính trùng
(kể cả recipe đã xóa mềm). Cả hai đều kiểm tra trước rồi mới chèn: hai request cùng tiêu đề đến gần như cùng lúc
cùng thấy slug còn trống, request lưu sau vi phạm unique index `IX_Recipes_Slug` (`23505`) → `DbUpdateException`
không được map → **500**. SRS không nói gì về tạo đồng thời.

**Chốt:**

1. `IRecipeRepository.TryAddAsync(recipe)` thêm và lưu recipe. Nếu lỗi là `23505` trên đúng `IX_Recipes_Slug` thì gỡ
   cả graph recipe khỏi change tracker và trả `false`; lỗi khác ném tiếp như cũ.
2. `CreateRecipeCommandHandler` thử **tối đa 5 lần** (N request cùng tiêu đề thì request xui nhất thua tối đa N−1 vòng): sinh slug (D10/D72) → dựng recipe → `TryAddAsync`. Trả `false`
   thì sinh lại slug — lần này thấy slug của request đã thắng nên ra `-2`, `-3`…
3. Hết 5 lần vẫn trùng → ném lỗi, middleware trả 500 (bất thường thật, cần log để xem).
4. Không khóa bảng, không advisory lock. Không đổi `SaveChangesAsync` chung của DbContext.

**Vì:** giữ đúng D10 (không 409, không 500) khi có request đồng thời. Race này hiếm nên thử lại chỉ tốn thêm khi thật
sự trùng; khóa thì mọi lệnh tạo recipe phải chờ nhau. Bắt lỗi ở repository (giống `SaveStepsAsync`, D73) để không
ảnh hưởng các lệnh ghi khác (Identity, refresh token).

**Sửa SRS:** FR-RCP-003 — thêm "Slug duy nhất kể cả khi nhiều request tạo cùng lúc".

---

## D86 — LCP của `/search`: không stream kết quả qua Suspense; cách đo Lighthouse CI (NFR-PERF-005)

> 2026-11-09. PR #86 (ảnh seed thật) làm job Lighthouse CI đỏ. Plan: `docs/plans/NFR-PERF-005-search-lcp.md`.

**Nguồn thiếu:** SRS 5.1 chỉ ghi `/search` là SSR, không nói kết quả render một lần hay stream. Trang bọc kết quả
trong `<Suspense>` để có skeleton. Khi card có ảnh thật, LCP `/search?q=ga` trên runner CI là 2607–2957ms (5/5 lần
vượt 2500ms):

- Kết quả bị stream nằm trong `<div hidden id="S:0">`, chỉ hiện khi script `$RC`/`$RV` của React chạy (React 19 gom
  và hoãn việc hiện boundary). Ảnh card đầu tải xong ở 40ms nhưng được vẽ ở ~194ms, sau khi JS hydrate.
- Lighthouse mô phỏng tính mọi request trước thời điểm LCP quan sát được → cộng cả ~180 KB JS và các prefetch RSC.
- Trang chi tiết recipe không stream nên LCP cũng là ảnh mà chỉ ~1.9s.

`/recipes` và `/recipes/banh-khot` fail ở các run trước chỉ vì lhci chọn trúng một lần đo lệch (1/5 lần) làm "run
đại diện"; các lần còn lại ~1.9–2.0s.

**Chốt:**

1. `/search` **không** bọc `SearchResults` trong `<Suspense>` (và không có `loading.tsx` phía trên): server chờ kết
   quả (API search có cache Redis, ~4ms) rồi gửi trong HTML đầu tiên.
2. Báo đang tải khi đổi từ khóa / trang (NFR-USE-004) thay cho skeleton: `SearchForm` có sẵn "Đang tìm…";
   `SearchPagination` chuyển trang trong `startTransition`, `Pagination` có prop `pending` (`aria-busy`, khóa nút) +
   dòng "Đang tải trang…". Kết quả cũ vẫn hiện trong lúc chờ — không bao giờ trống trang.
3. Lighthouse CI: làm nóng cache trước khi đo (`frontend/scripts/lhci-server.mjs`) và artifact giữ `.lighthouseci`
   (`include-hidden-files: true`) để xem được báo cáo. Ngưỡng giữ nguyên 2500ms.
4. Ảnh card đầu của lưới (`RecipeCard isLcp`) dùng `preload` — `<link rel="preload">` trong `<head>`, giống ảnh đầu
   trang chi tiết (`priority`). Không có thì trình duyệt chỉ thấy ảnh ở thẻ `<img>` sau CSS: run CI 42597856560 đo
   Load Delay 435ms ở `/search` so với 0ms ở trang chi tiết. Card khác `lazy`.
5. Đã thử, không thấy hiệu quả rõ nhưng vô hại nên giữ: ảnh `RecipeCard` quality 50 (có trong `images.qualities`),
   `sizes` khớp lưới 1/2/3 cột (D84).

**Theo dõi:** `/recipes` cùng mẫu Suspense. Đang pass vì trên mobile LCP của nó là chữ trong header; nếu ảnh card thành
LCP (vd. bỏ bớt bộ lọc phía trên) thì áp dụng như mục 1.

**Vì:** sửa đúng cơ chế làm LCP muộn mà không hạ chuẩn; trang kết quả tìm kiếm vẫn SSR đúng SRS 5.1.

**Sửa SRS:** NFR-PERF-005 — thêm "đo khi cache đã nóng (mỗi URL đã được tải một lần)".

---

## D87 — Lighthouse CI chấm theo trung vị 9 lần đo (NFR-PERF-005)

> 2026-11-09. Phát hiện khi đọc báo cáo của 3 run CI liền nhau trên PR #86 (D86).

**Nguồn thiếu:** SRS ghi ngưỡng (LCP < 2.5s…) nhưng không nói tổng hợp nhiều lần đo thế nào. `lighthouserc.json` dùng
`aggregationMethod: "median-run"`: lhci chọn **một** lần chạy "đại diện" (theo FCP/TTI) rồi chấm mọi chỉ số của lần
đó. Lần được chọn có thể là lần lệch về LCP: trong cùng một run, `/` đo 1889–2806ms, `/recipes/banh-khot` 1886–2495ms,
`/categories/mon-khai-vi` 1810–2417ms. `/recipes` và `/recipes/banh-khot` từng làm job đỏ chỉ vì 1/5 lần lệch.

**Chốt:**

1. `aggregationMethod: "median"` — mỗi chỉ số lấy trung vị của **9** lần đo (`numberOfRuns: 9`, trước là 5). Ngưỡng giữ
   nguyên (LCP 2500ms, CLS 0.1, TBT 200ms, script 200 KB).
2. Kiểm lại trên báo cáo của 3 run CI (42593569015, 42597856560, 42600886432): chỉ còn `/search?q=ga` vượt —
   trung vị 2801 → 2679 → 2604ms qua các bản sửa của D86. Các trang khác đều dưới ngưỡng.
3. `/search` còn vượt ~100ms ở thời điểm đó → xử lý bằng ảnh card cắt đúng khung (D88): trung vị 2486ms ở run
   42614466149.

4. **9 lần thay vì 5** (bổ sung 2026-11-09, run 42614466149 của PR #88): trên runner CI mọi trang dao động giữa hai
   mức, ~1.8–2.0s hoặc ~2.4–2.9s, kể cả trang mà LCP là chữ (`/`: 1962 · 1962 · 1965 · 2654 · 2949). Với 5 lần, trung vị
   pass/fail tùy số lần rơi vào mức chậm: `/recipes/banh-khot` (không đổi code) trung vị ~1.89s ở 3 run trước nhưng
   2502ms ở run này (4/5 lần chậm). Job Lighthouse lâu thêm ~4–5 phút.

**Vì:** một lần đo lệch không quyết định kết quả nữa, nhưng không nới ngưỡng và không che `/search` đang vượt thật.

**Sửa SRS:** NFR-PERF-005 — thêm "chấm trên trung vị 9 lần đo Lighthouse".

---

## D88 — FR-JOB-002: biến thể ảnh `card` cắt đúng khung card (NFR-PERF-005)

> 2026-11-09. Tiếp theo D86/D87 (LCP `/search` trung vị 2604ms). Plan: `docs/plans/FR-JOB-002-card-variant.md`.
> Bổ sung D40 (D40 chỉ có medium + thumbnail).

**Nguồn thiếu:** Card hiển thị dải ảnh cao 160px (`object-cover`), rộng ~360–380px (tỉ lệ ~2.4:1) nhưng nhận ảnh 4:3 —
hơn nửa điểm ảnh bị CSS che. `/_next/image` chỉ đổi kích thước, không cắt. Đo trên 3 ảnh đầu của `/search?q=ga` (WebP
q50, rộng 750): 4:3 = 108 KB, cắt 750×280 = 67 KB (−38%). AVIF chỉ −5% nên không dùng.

**Chốt:**

1. FR-JOB-002 sinh thêm biến thể **card**: crop giữa theo tỉ lệ 760:320 rồi thu về **760×320**, WebP (D41); vùng cắt
   hẹp hơn 760 thì giữ nguyên rộng, không phóng to (như D40).
2. `RecipeImage.CardUrl` (nullable, migration `AddRecipeImageCardUrl`). Xóa ảnh xóa cả file card (D43).
3. Idempotent (D44) theo `CardUrl`: đã có card → no-op. Ảnh xử lý trước D88 (có medium + thumbnail, chưa có card) →
   job chỉ upload card, giữ URL cũ, không sinh file mồ côi.
4. `RecipeSummaryDto.FeaturedImageCardUrl` = card của đúng ảnh đại diện (primary, không có thì ảnh đầu).
   `FeaturedImageUrl` giữ là ảnh gốc.
5. `RecipeCard` dùng `featuredImageCardUrl ?? featuredImageUrl` — chưa có card vẫn hiện ảnh gốc như cũ.
6. DbSeeder (Development): ảnh của recipe seed đã có thumbnail mà chưa có card → enqueue job để bổ sung.

**Ngoài phạm vi:** ảnh người dùng đã upload trên production trước D88 chưa có card tới khi được xử lý lại (card dùng
ảnh gốc, không hỏng). Backfill production cần một job/lệnh riêng.

**Vì:** người dùng thấy y hệt mà ảnh card nhẹ hơn ~38% trên mọi trang có card (`/`, `/recipes`, `/search`, danh mục).

**Sửa SRS:** FR-JOB-002 — "sinh medium 800×600, thumbnail 300×300 **và card 760×320**"; mục 7.5 thêm cột `CardUrl`.

---

## D89 — NFR-REL-001: phạm vi kiểm được trong v1 (uptime, giám sát ngoài, banner bảo trì)

> 2026-11-09. Phát hiện khi rà việc tuần 8 của D (traceability NFR-REL-001 còn 🟡).

**Nguồn thiếu:** SRS NFR-REL-001 gộp 4 yêu cầu: uptime ≥ 99.5%/năm, readiness probe `/health/ready` mỗi 10s,
Uptime Robot / Better Uptime báo động khi sập > 1 phút, banner báo bảo trì trước 48 giờ. Ba yêu cầu sau phần probe
cần hệ thống chạy công khai nhiều tháng (uptime), một URL public (giám sát ngoài), hoặc một tính năng không có FR nào
mô tả (banner — ai bật, cấu hình ở đâu). Không có FR nào để truy code về (Quy tắc vàng 2).

**Chốt:**

1. **Trong phạm vi v1:** health check 3 endpoint (FR-OBS-001) + healthcheck container `api` gọi `/health/ready`
   mỗi 10s trong `docker-compose.prod.yml`. Đã có → NFR-REL-001 tính ✅ theo phạm vi này.
2. **Ngoài phạm vi v1:** chứng minh uptime ≥ 99.5%; banner báo bảo trì 48 giờ (không code).
3. **Tùy chọn khi deploy công khai:** đăng ký Uptime Robot trỏ vào `https://{SITE_DOMAIN}/health/ready`, báo
   động khi sập > 1 phút — chỉ là cấu hình dịch vụ ngoài, không đổi code.

**Vì:** đồ án 8 tuần không có môi trường chạy dài hạn để đo SLA; phần kiểm được bằng code/test đã xong. Banner bảo
trì là tính năng mới không có FR, thêm vào là tự mở rộng scope (CLAUDE.md mục 9).

**Sửa SRS:** NFR-REL-001 — tách "readiness probe 10s" (v1) khỏi "uptime 99.5%, Uptime Robot, banner bảo trì"
(vận hành sau khi deploy).

---

## D90 — NFR-REL-002: Redis tắt không làm chậm request, Redis lên lại không trả cache cũ

> 2026-11-10. Phát hiện bằng chaos test `IntegrationTests/Resilience/RedisDownTests.cs` (plan
> `plans/NFR-REL-002-redis-down.md`).

**Nguồn thiếu:** SRS NFR-REL-002 chỉ ghi "Redis down → fallback database (không cache), không throw exception".
D8 chốt "Redis down → log warning rồi đi thẳng xuống handler". Code đã không throw, nhưng chaos test cho thấy hai
chỗ SRS và D8 không nói tới:

1. **Chậm:** StackExchange.Redis mặc định xếp lệnh vào backlog chờ kết nối lại, tới `AsyncTimeout` 5 s mới báo lỗi.
   Đo được: mỗi `GET /categories` và `POST /categories` mất **5,4–5,9 s** khi Redis tắt — không sập nhưng với
   người dùng thì giống sập, và vượt xa p95 ≤ 500 ms (NFR-PERF-001).
2. **Cache cũ:** ghi dữ liệu lúc Redis tắt → `CacheInvalidationBehavior` xóa tag thất bại (đúng D8: nuốt lỗi).
   Redis lên lại với dữ liệu cũ (persistence) → API trả bản cũ tới hết TTL — `categories:all` tới **30 phút**.
   Test: danh mục tạo lúc Redis tắt không hiện ở `GET /categories` sau khi Redis lên lại.

**Chốt:**

1. Multiplexer (`Infrastructure/DependencyInjection.cs`): `BacklogPolicy.FailFast` — mất kết nối thì lệnh cache lỗi
   ngay, `CachingBehavior` đi thẳng xuống DB. `AsyncTimeout`/`SyncTimeout` = **500 ms** cho trường hợp Redis còn
   kết nối nhưng không phản hồi (lệnh cache bình thường < 10 ms). `ConnectTimeout` giữ mặc định 5 s — đặt 1 s thì
   trên Docker Desktop/Windows multiplexer không kết nối được Redis cả khi Redis đang chạy (đã thử).
2. `Infrastructure/Caching/RedisCacheRecovery.cs`: sự kiện `ConnectionRestored` (kết nối Interactive) → quét mọi tag
   set `tag:*` bằng SCAN, xóa key trong đó và chính tag set. Mọi key cache đều có ít nhất một tag (bảng D8) nên quét
   tag là đủ. Không throw; lỗi → log Warning, cache tự hết hạn theo TTL.
3. **Không** giữ danh sách "tag đã lỡ xóa" trong bộ nhớ API: nhiều instance thì instance khác cũng phải dọn được, và
   NFR-SCALE-001 yêu cầu stateless.

**Hạn chế đã biết (chấp nhận):**

- Từ lúc kết nối lại tới khi dọn xong (vài chục ms) request vẫn có thể đọc bản cũ.
- Một request đọc DB **trước** lệnh ghi nhưng ghi cache **sau** khi dọn xong có thể nạp lại bản cũ — cửa sổ rất
  hẹp, tự hết theo TTL.
- API khởi động khi Redis đang tắt: request đầu tiên dùng cache chờ tối đa `ConnectTimeout` (5 s) một lần duy nhất
  để tạo multiplexer; các request sau FailFast.
- Kịch bản Redis "treo" (kết nối mở nhưng không phản hồi) không có test tự động — chỉ dựa vào timeout 500 ms.

**Vì:** Redis chỉ là cache; mọi dữ liệu đúng đều nằm ở PostgreSQL. Bỏ cache một lúc chỉ tốn thêm truy vấn DB, còn
chờ 5 s hoặc trả dữ liệu sai 30 phút thì người dùng thấy ngay.

**Sửa SRS:** NFR-REL-002 — thêm "lệnh cache thất bại phải trả về trong ≤ 500 ms" và "khi kết nối lại Redis, xóa
cache có thể đã cũ".

---

## D91 — NFR-SEC-005: cookie phiên giữ `SameSite=Lax`

> 2026-11-10.

**Nguồn:** SRS NFR-SEC-005 ghi "cookie SameSite=Strict".

**Chốt:** cookie của Auth.js (session JWT mã hóa, `httpOnly`, `Secure` trên HTTPS) giữ mặc định **`Lax`**.

**Vì:**

1. Đăng nhập Google (FR-AUTH-003) chạy qua provider Google của Auth.js (`frontend/auth.ts`). Khi Google redirect về
   `/api/auth/callback/google`, đó là điều hướng **cross-site**; với `Strict` trình duyệt không gửi cookie `state` /
   `pkce.code_verifier` → Auth.js báo `InvalidCheck`, không đăng nhập được bằng Google.
2. `Strict` còn làm người dùng bấm link từ email chào mừng (FR-JOB-001 → `/dashboard`) thấy như chưa đăng nhập ở lần
   mở đầu tiên.
3. Rủi ro CSRF mà `Strict` muốn chặn đã được xử lý chỗ khác: API backend xác thực bằng header `Authorization: Bearer`
   (không đọc cookie) nên không bị CSRF; các route POST của Auth.js có CSRF token riêng. `Lax` vẫn không gửi cookie
   trên request POST cross-site.

**Sửa SRS:** NFR-SEC-005 — "cookie `SameSite=Lax` (Strict không tương thích OAuth redirect)".

---

## D92 — NFR-SEC-006: "audit log mọi write" trong v1

> 2026-11-10.

**Nguồn:** SRS NFR-SEC-006 ghi "audit log mọi write" nhưng không nói lưu ở đâu, giữ bao lâu, gồm trường nào.

**Chốt:** audit log v1 là **log có cấu trúc**, không phải bảng riêng:

- Mọi request HTTP — gồm mọi POST/PUT/PATCH/DELETE — có một dòng request log với method, path, status, thời gian,
  `CorrelationId`, và `UserId` khi đã đăng nhập (FR-OBS-002, CONS-010 — test `Observability/StructuredLoggingTests.cs`).
  Ai, làm gì, lúc nào, kết quả ra sao đều tra được trong Seq / file log.
- `LoggingBehavior` ghi thêm tên Command (`CreateRecipeCommand`…) cùng `CorrelationId` → nối được request với use case.
- Thao tác khó hoàn tác — xóa, publish/unpublish, archive recipe — ghi thêm log riêng có `{RecipeId}` / `{UserId}` (D77, D78).

**Không làm trong v1:** bảng `AuditLog` trong DB, giao diện xem audit, lưu giá trị trước/sau khi sửa.

**Vì:** SRS không có FR nào đọc audit log (không có màn hình, không có báo cáo) — một bảng không ai đọc chỉ thêm việc
ghi mỗi request. Log có cấu trúc đã đủ trả lời "ai làm gì lúc nào" khi điều tra sự cố.

**Sửa SRS:** NFR-SEC-006 — "audit log = structured request log có UserId cho mọi lệnh ghi".

---

## D93 — NFR-SEC-007: khóa JWT cũ còn trong lịch sử Git

> 2026-11-10.

**Nguồn:** NFR-SEC-007 "không secret trong Git". `Jwt:Key` từng nằm trong `appsettings.json` ở các commit đầu
(đã bỏ ở CONS-009, xem traceability).

**Chốt:** **không** viết lại lịch sử Git (`git filter-repo` / force push). Coi khóa đó là **đã lộ**: chỉ dùng cho dev;
production bắt buộc sinh khóa mới bằng `openssl rand -base64 48` (`docs/deployment.md`, `.env.production.example`).
`JwtKeyGuard` chặn API khởi động khi thiếu khóa hoặc khóa < 32 byte. Hook `gitleaks` (`.githooks/pre-commit`) và job CI
"Quét secret" chặn secret mới.

**Vì:** viết lại lịch sử buộc cả 4 người clone lại, phá các nhánh và PR đang mở, mà không thu hồi được bản đã có trên
máy người khác hay trên GitHub. Đổi khóa mới hiệu quả hơn và không tốn gì.

**Sửa SRS:** không cần.

---

## D94 — NFR-SCALE-003: CDN cho tài nguyên tĩnh ngoài phạm vi v1

> 2026-11-10.

**Nguồn:** NFR-SCALE-003 "CDN static assets".

**Chốt:** v1 **không** gắn CDN. Tài nguyên tĩnh đã có header cache để gắn CDN sau mà không phải sửa code:

- `/_next/static/*`: Next.js trả `Cache-Control: public, max-age=31536000, immutable` (tên file có hash).
- Ảnh recipe `/culinary-blog/*`: tên object là GUID (D16), Nginx `expires 30d` (`nginx/nginx.prod.conf`).

**Vì:** nhóm không có domain thật và ngân sách CDN cho đồ án; gắn CDN (Cloudflare…) là cấu hình DNS / origin, không
thuộc code.

**Sửa SRS:** NFR-SCALE-003 — chuyển "CDN static assets" sang "khuyến nghị khi triển khai thật".

---

## D95 — NFR-PERF-002: "2 vCPU / 4 GB" là giới hạn của instance API

> 2026-11-10.

**Nguồn:** NFR-PERF-002 "≥ 100 concurrent users trên 2 vCPU / 4GB" — không nói 2 vCPU là cho API hay cả hệ thống.

**Chốt:** hiểu là **một instance API** (container `api`). PostgreSQL và Redis là service riêng (NFR-SCALE-003: mỗi
service một container) và được tính tài nguyên riêng.

**Đã đo** (`backend/tests/performance/results/perf-summary.md`, 2026-11-03): API giới hạn `cpus: 2`, `mem_limit: 4g` —
100 VU 0% lỗi, p99 100,9 ms; 180 VU 0% lỗi, p99 424,8 ms; RAM tối đa ~515 MiB.

**Chưa đo:** cả API + PostgreSQL + Redis cùng chia 2 vCPU / 4 GB (một VPS nhỏ chạy hết). Nếu triển khai kiểu đó thì
phải đo lại.

**Sửa SRS:** NFR-PERF-002 — "2 vCPU / 4 GB cho mỗi instance API".

---

## D96 — NFR-MAINT-001: analyzer có sẵn của .NET thay SonarAnalyzer / StyleCop; Prettier

> 2026-11-10.

**Nguồn:** NFR-MAINT-001 "0 compiler warning, SonarAnalyzer/StyleCop, ESLint + Prettier".

**Chốt:**

1. **Backend:** không thêm SonarAnalyzer / StyleCop. Dùng analyzer đi kèm .NET SDK (`AnalysisLevel=latest`) +
   `TreatWarningsAsErrors=true` + `Nullable=enable` (`Directory.Build.props`). CI bước "Build (0 warning)" đỏ khi có
   warning bất kỳ.
2. **Frontend:** ESLint (`eslint-config-next`) chạy trong CI. Prettier có cấu hình (`frontend/.prettierrc`, script
   `npm run format`) nhưng CI **chưa** kiểm: ngày 2026-11-10 `prettier --check --end-of-line auto app components lib`
   báo 59 file chưa format.

**Việc còn lại (sau demo):** chạy `npm run format` một lần trong PR riêng (chạm file của nhiều người — làm khi không
còn nhánh đang mở), thêm `.prettierignore` và bước `npx prettier --check .` vào job CI Frontend.

**Vì:** thêm StyleCop/Sonar lúc cuối dự án sinh hàng trăm cảnh báo về style, và với `TreatWarningsAsErrors` build sẽ
đỏ — phải sửa hàng loạt file ngay trước demo mà không tăng chất lượng thực. Format hàng loạt bằng Prettier cũng vậy.

**Sửa SRS:** NFR-MAINT-001 — "analyzer .NET SDK + TreatWarningsAsErrors" thay "SonarAnalyzer/StyleCop".

---

## D97 — Bộ nhận diện thị giác: tông tối, "sổ tay nấu ăn" (NFR-USE-001/002)

> 2026-11-10. Plan: `docs/plans/D97-giao-dien-so-tay.md`. Mockup: https://claude.ai/artifact/9B9gVDKQ5wujwRTP9wrBUJ

**Nguồn thiếu:** SRS chỉ ràng buộc rendering (bảng 5.1), WCAG 2.1 AA (NFR-USE-002), responsive (NFR-USE-001) và hiệu
năng (NFR-PERF-005) — không nói gì về màu, font, bố cục. Đề tài chung cho > 30 nhóm; bố cục mặc định (header ngang →
hero → lưới thẻ 3 cột, nền trắng + cam) dễ trùng với nhóm khác.

**Chốt:**

1. **Màu (token `app/globals.css`, giữ nguyên tên token):** nền `#1A1310`, bề mặt `#241A16`, viền `#3A2C25`, chữ
   `#F7EBDD` / phụ `#CDB8A6`, brand `#E8693F` (hover/link `#F2936D`), nhấn vàng `#F2B544`. Chữ trên nền brand dùng
   token mới `on-brand` (`#1A1310`) — chữ trắng trên `#E8693F` chỉ ~3:1. Màu trạng thái thành token: `danger`,
   `success`, `warning` (chữ / nền nhạt / viền). Bảng contrast trong `globals.css`. Chỉ có giao diện tối
   (`color-scheme: dark`), không có công tắc sáng/tối.
2. **Font:** chỉ **font hệ thống** (tiêu đề cỡ lớn, `font-extrabold`) — không web font.
   - *D97.2 (2026-11-10, đo Lighthouse trên cùng máy, so với main):* bản đầu dùng *Baloo 2* (tiêu đề) + *Be Vietnam Pro*
     (nội dung) qua `next/font` → 107–150 KB font / trang, LCP trung vị 3.7–4.4s; chỉ Baloo 2 (`display: optional`,
     có hoặc không preload) → 43–70 KB, LCP 3.0–3.6s; main (không web font) → 2.8–3.0s. LCP mọi trang là chữ; JS / CSS /
     HTML của D97 tương đương main. Web font nào cũng cộng ~300–600ms ở lần xem đầu → bỏ (người dùng chọn 2026-11-10).
3. **Icon:** niêu đất (`app/icon.svg`, logo trên thanh bên).
4. **Bố cục:** header ngang → thanh bên kiểu mục lục (danh mục đánh số); trang chủ dạng thực đơn có đường chấm dẫn;
   trang chi tiết có dòng thời gian nấu theo phút (cộng dồn `timerMinutes`). `/recipes`, `/search`, `/categories/*`,
   dashboard, auth chỉ đổi màu, giữ bố cục.
5. Giữ nguyên chữ trên link điều hướng, landmark và `aria-label` (E2E, NFR-USE-002); giữ neo `#buoc-n` (NFR-SEO-001).
6. **Tỉ lệ khung ảnh (2026-11-11):** ảnh món chủ yếu 4:3. Ảnh nhỏ "Thực đơn mới" (vuông) lấy từ ảnh **gốc**, không
   lấy biến thể card 760×320 (đã cắt sẵn → cắt vuông lần nữa chỉ còn ~24% ảnh; từ ảnh gốc giữ ~75%). Ảnh lớn trang chi
   tiết dùng khung **16:10** thay chiều cao cố định 380px (~900×380 chỉ giữ ~56% ảnh; 16:10 giữ ~83%). Thẻ món ở
   `/recipes`, `/search` giữ biến thể card của D88 (đổi khung cần đo lại LCP — chưa làm).
7. **Công thức chưa có ảnh (2026-11-11):** SRS không bắt buộc ảnh để xuất bản (D3), 6/50 món seed không có ảnh giấy
   phép phù hợp (`SeedImageAssets`). Thay khung xám "Chưa có hình ảnh" bằng `RecipeImagePlaceholder`: nền theo slug
   (6 tông ấm, cùng món luôn cùng màu), chữ cái đầu tên món, icon niêu đất mờ; "Chưa có hình ảnh" giữ cho trình đọc
   màn hình (`sr-only`). Dùng ở thẻ món, thực đơn, Món đáng thử, trang chi tiết (không ảnh → băng ngang thấp thay khung
   16:10). Không tải file nào thêm. axe 0 vi phạm.

**Vì:** khác biệt rõ với các nhóm khác mà không đổi API, không thêm FR; đi qua token nên ít file phải sửa. Kiểm lại
bằng axe (D83), Lighthouse (D87) và E2E sau mỗi bước.

**Sửa SRS:** không (SRS không quy định giao diện).

---

## D98 — Ảnh món seed: nguồn kết hợp, thay ảnh seed cũ trên DB dev (DEV-SEED)

> 2026-11-13. Plan: `docs/plans/DEV-SEED-anh-mon-an.md` (mục "D98").

**Nguồn thiếu:** SRS chỉ yêu cầu dữ liệu mẫu (dòng 295), không nói về ảnh. Bản đầu (2026-11-08) chỉ lấy Wikimedia
Commons: phần lớn ảnh chụp nghiệp dư, một số **sai món** (canh nấm *chay* là bò hầm Hàn Quốc, cơm chiên chay kiểu
Kerala, chả giò chay có watermark blog), 6/50 món không có ảnh. Seeder bỏ qua recipe đã có ảnh → DB dev đã seed không
nhận ảnh mới nếu chỉ thay file.

**Chốt:**

1. **Nguồn:** Pexels, Unsplash (giấy phép cho dùng tự do, không bắt buộc ghi công) và Wikimedia Commons (CC0 / PD /
   CC BY / CC BY-SA, ghi công theo giấy phép). **Không dùng ảnh AI.** `CREDITS.md` vẫn ghi link gốc mọi ảnh.
2. **Chọn ảnh:** ưu tiên đúng món; không có ảnh đúng món thì lấy món gần giống nhưng không trái nội dung (món chay
   không có thịt). Không có ảnh chấp nhận được → để trống (khung minh họa D97 mục 7). Kết quả: 37 ảnh thay, 5 món mới
   có ảnh, 7 ảnh Commons giữ nguyên (không tìm được ảnh tốt hơn), 1 món trống (`goi-ngo-sen-tom-thit`).
3. **Định dạng không đổi:** WebP 1200×900 (4:3), tên `{slug}.webp`, ~5 MB tổng. Ảnh dọc cắt theo vị trí chọn tay cho
   món nằm giữa dải card 760×320 (D88).
4. **DB dev đã seed:** `SeedImageAssets.RetiredSha256` giữ SHA-256 của 37 ảnh cũ. Mỗi lần seed, recipe seed có đúng 1
   ảnh, alt = tên món và nội dung trùng một hash cũ → gỡ ảnh, enqueue xóa file gốc + biến thể (D43), gắn ảnh mới,
   enqueue tạo biến thể (FR-JOB-002). Nhận diện theo nội dung nên ảnh người dùng upload không bao giờ khớp; object
   không đọc được → để nguyên.
5. *D98.2 (2026-11-13, người dùng xem trên web):* thẻ món (biến thể card 760×320 ≈ 2,4:1, D88) chỉ giữ dải giữa
   ~56% chiều cao ảnh → ảnh cận (thịt kho, rau xào) và ly cao (trà sữa, sinh tố) mất chủ thể. Thịt kho, rau xào đổi
   sang ảnh có món nằm gọn giữa khung; trà sữa, sinh tố (nền tối trơn) thu nhỏ cả ly đặt giữa khung, mép ảnh mờ dần
   vào màu nền — cả ly lọt dải card. Hash bản trước thêm vào `RetiredSha256`. **Quy tắc chọn ảnh từ nay:** xem cả dải
   2,4:1 ở giữa, không chỉ khung 4:3.
6. **Frontend dev sau khi seeder thay ảnh:** seeder ghi thẳng DB nên không gọi `revalidateTag('recipes')`; trang ISR
   (`/` 3600s…) giữ URL ảnh cũ — file cũ đã xóa nhưng `next/image` vẫn trả bản tối ưu đã cache. Next dev lưu cache
   ở `frontend/.next/dev/cache/` (không phải `.next/cache/`) và giữ qua lần khởi động lại. Xử lý: tắt `npm run dev`,
   xóa `frontend/.next/dev/cache/fetch-cache` và `.../images`, chạy lại (hoặc chờ hết `revalidate`).

**Vì:** ảnh mẫu là thứ đầu tiên người xem thấy khi demo; thay bằng ảnh đúng món, chụp chuyên nghiệp mà không đổi
API, schema hay frontend. Không bắt cả nhóm xóa volume dev.

**Đánh đổi:** seeder tải lại ảnh của mỗi recipe seed (~50 × ≤ 300 KB, S3 local) mỗi lần API Development khởi động để
tính hash. Lịch sử Git giữ cả bộ ảnh cũ (~4 MB).

**Sửa SRS:** không.

---

## Khi phát hiện mâu thuẫn mới lúc code

1. Thêm mục mới vào cuối file này với mã `D23`, `D24`…
2. Ghi: nguồn mâu thuẫn (trích trang SRS) → quyết định → lý do → cái gì phải sửa trong SRS.
3. Cập nhật `traceability.md` nếu nó ảnh hưởng FR nào.
4. Nếu là quyết định **kiến trúc** (không phải làm rõ SRS) thì viết ADR trong `docs/adr/`.
