# Ma trận Traceability — Culinary Blog v1.0.0

Ánh xạ **yêu cầu → hiện thực → bằng chứng kiểm thử**. Cập nhật sau mỗi slice.

**Trạng thái:** `⬜ Chưa làm` · `🟡 Đang làm` · `✅ Xong`

> **Tổng số FR là 34** (D21), không phải 27 như SRS Chương 1.5 và 3 ghi nhầm:
> AUTH 7 + CAT 5 + RCP 10 + SRCH 4 + FILE 2 + JOB 3 + OBS 3.
>
> Cột **Quyết định** trỏ tới mục trong `decisions.md` mà FR đó phải tuân theo khi
> nó khác với SRS. Đọc mục đó **trước** khi code FR.

> **Ai phụ trách FR nào:** xem `team-assignment.md` mục 2.
> Tóm tắt: **A** = FR-AUTH + FR-JOB-001 · **B** = FR-CAT + FR-RCP-001/002 + FR-SRCH ·
> **C** = FR-RCP-003/004/005/006/007/009/010 · **D** = FR-RCP-008 + FR-FILE + FR-JOB-002/003 + FR-OBS.

**Tiến độ:** 8 / 34 FR (24%) — A 2/8 · B 3/11 · C 0/7 · D 3/8 (FR-RCP-008, FR-FILE-001/002 —
FR-JOB-002 cố ý để lại, xem ghi chú ở mục FR-JOB)

---

## FR-AUTH — Xác thực & Quản lý Người dùng (7 FR)

| FR | Tên | Ưu tiên | Endpoint | Slice | Application Layer | Infrastructure / Domain | Test | Quyết định | TT |
|---|---|---|---|---|---|---|---|---|---|
| FR-AUTH-001 | Đăng ký tài khoản | M | `POST /api/v1/auth/register` → 201 | S2 | `RegisterCommand(+Handler,+Validator)` | `ApplicationUser` (Infrastructure, D23), `JwtService`, `RefreshToken` (Domain, D26) | `Auth/RegisterTests.cs`, `Auth/RegisterCommand{Validator,Handler}Tests.cs`, `Domain/RefreshTokenTests.cs` | D5, D20, D23, D24, D25, D26, D29 | ✅ |
| FR-AUTH-002 | Đăng nhập email/password | M | `POST /api/v1/auth/login` → 200 | S2 | `LoginCommand(+Handler,+Validator)` | `IdentityService.ValidateCredentialsAsync` (lockout + IsActive) | `Auth/LoginTests.cs`, `Auth/LoginCommand{Validator,Handler}Tests.cs` | D4, D5, D11, D17, D20, D24, D26 | ✅ |
| FR-AUTH-003 | Đăng nhập Google OAuth | S | `POST /api/v1/auth/google` → 200 | S9 | `GoogleLoginCommand(+Handler)` | Google ID Token verify, Auth.js v5 (FE) | `Auth/GoogleLoginTests.cs` | **D9**, D5 | ⬜ |
| FR-AUTH-004 | Làm mới access token | M | `POST /api/v1/auth/refresh` → 200 | S2 | `RefreshTokenCommand(+Handler)` | `RefreshTokenRepository` (rotation + reuse detection) | `Auth/RefreshTokenTests.cs` | **D20**, D11 | ⬜ |
| FR-AUTH-005 | Đăng xuất / revoke | M | `POST /api/v1/auth/logout` → 204 | S2 | `LogoutCommand(+Handler)` | `RefreshTokenRepository` | `Auth/LogoutTests.cs` | D20 | ⬜ |
| FR-AUTH-006 | Xem hồ sơ cá nhân | S | `GET /api/v1/auth/me` → 200 | S2 | `GetCurrentUserQuery(+Handler)` | `ICurrentUser` | `Auth/ProfileTests.cs` | **D5**, D12 | ⬜ |
| FR-AUTH-007 | Cập nhật hồ sơ | S | `PATCH /api/v1/auth/me` → 200 | S2 | `UpdateProfileCommand(+Handler,+Validator)` | `UserManager` | `Auth/ProfileTests.cs` | **D5** | ⬜ |

**Điểm kiểm thử bắt buộc**

- Email trùng → 409 `AUTH_EMAIL_EXISTS`
- Sai mật khẩu → 401 message generic (không lộ email có tồn tại hay không)
- Sai 5 lần → lockout 15 phút → **423 `AUTH_ACCOUNT_LOCKED`** (D17)
- `IsActive = false` → **403 `AUTH_ACCOUNT_DISABLED`** ở cả login và refresh (D11)
- Refresh token đã revoke dùng lại → 401 + **LOG WARNING** + revoke cả token family (D20)
- Logout với refresh token không tồn tại → vẫn **204** (idempotent)
- Register/profile chỉ nhận `displayName`, `avatarUrl`, `bio` — gửi `fullName` bị bỏ qua (D5)
- Google login với email trùng tài khoản đã đăng ký thủ công → **liên kết**, không tạo trùng (D9)
- Validation fail → **400 `VALIDATION_ERROR`** (D4 — SRS Chương 3 ghi 422, sai)

---

## FR-CAT — Quản lý Danh mục (5 FR)

| FR | Tên | Ưu tiên | Endpoint | Slice | Application Layer | Infrastructure / Domain | Test | Quyết định | TT |
|---|---|---|---|---|---|---|---|---|---|
| FR-CAT-001 | Xem danh sách danh mục | M | `GET /api/v1/categories` → 200 | S3 | `GetCategoriesQuery(+Handler)` `ICacheable` | `CategoryRepository.GetAllWithRecipesAsync()` | `Categories/GetCategoriesTests.cs` | **D8** | ✅ |
| FR-CAT-002 | Chi tiết danh mục + recipes | M | `GET /api/v1/categories/{slug}` → 200/404 | S3 | `GetCategoryBySlugQuery(+Handler)` | `CategoryRepository.GetBySlugAsync()` (D32) | `Categories/GetCategoryBySlugTests.cs` | D8, **D32** | ✅ |
| FR-CAT-003 | Tạo danh mục [Admin] | M | `POST /api/v1/categories` → 201 | S3 | `CreateCategoryCommand(+Handler,+Validator)` `ICacheInvalidator` | `Category.Create()`, `SlugHelper.Generate()` | `Categories/CreateCategoryTests.cs` | D10, D8 | ✅ |
| FR-CAT-004 | Cập nhật danh mục [Admin] | M | `PUT /api/v1/categories/{id}` → 200 | S3 | `UpdateCategoryCommand(+Handler,+Validator)` `ICacheInvalidator` | `Category.Update()`, Slug **KHÔNG** đổi khi đổi Name | `Categories/UpdateCategoryTests.cs`, `Categories/UpdateCategoryCommand{Validator,Handler}Tests.cs` | D4, D8, D10 | ✅ |
| FR-CAT-005 | Xóa danh mục [Admin] | S | `DELETE /api/v1/categories/{id}` → 204 | S3 | `DeleteCategoryCommand(+Handler)` | **Soft delete**, đếm recipe > 0 → 409 | `Categories/DeleteTests.cs` | **D2** | ⬜ |

**Điểm kiểm thử bắt buộc**

- Guest gọi POST → 401 · Author gọi POST → 403 · Admin → 201
- Name trùng → 409 `CATEGORY_NAME_EXISTS`; slug trùng → **auto-suffix** `-2`, `-3` (D10)
- Slug tiếng Việt đúng: "Món khai vị" → `mon-khai-vi`
- Xóa category còn recipe (kể cả Draft) → 409 `CATEGORY_DELETE_HAS_RECIPES`
- Xóa thành công → `IsDeleted = true`, bản ghi **vẫn còn trong DB**, biến mất khỏi mọi query (D2)
- Sau mỗi write: tag cache `categories` **và** `recipes` đều bị xóa (D8)
- Sửa tên category → `GET /recipes` ngay sau đó trả tên mới (bằng chứng cache đã invalidate)

---

## FR-RCP — Quản lý Công thức (10 FR)

| FR | Tên | Ưu tiên | Endpoint | Slice | Application Layer | Infrastructure / Domain | Test | Quyết định | TT |
|---|---|---|---|---|---|---|---|---|---|
| FR-RCP-001 | Danh sách công thức (paged/filter/sort) | M | `GET /api/v1/recipes` → 200 | S4 | `GetRecipesQuery(+Handler)` `ICacheable` | `RecipeRepository` IQueryable + authorization filter | `Recipes/ListTests.cs` | D8, D14 | ⬜ |
| FR-RCP-002 | Chi tiết công thức | M | `GET /api/v1/recipes/{slug}` → 200/403/404 | S4 | `GetRecipeBySlugQuery(+Handler)` `ICacheable` | Eager loading Steps/Ingredients/Images/Category/Author | `Recipes/DetailTests.cs` | D8 | ⬜ |
| FR-RCP-003 | Tạo công thức | M | `POST /api/v1/recipes` → 201 | S5 | `CreateRecipeCommand(+Handler,+Validator)` | `Recipe.Create()`, `SlugHelper` | `Recipes/CreateTests.cs` | **D10, D18, D19** | ⬜ |
| FR-RCP-004 | Cập nhật công thức | M | `PUT /api/v1/recipes/{id}` → 200 | S5 | `UpdateRecipeCommand(+Handler,+Validator)` | `RowVersion` concurrency, `RecipeAuthorizationHandler` | `Recipes/UpdateTests.cs` | **D4**, D13 | ⬜ |
| FR-RCP-005 | Publish / Unpublish | M | `PATCH /api/v1/recipes/{id}/publish` `/unpublish` → 200 | S7 | `PublishRecipeCommand(+Handler)` | `recipe.Publish()` ném `DomainException` | `Recipes/PublishTests.cs` | **D3** | ⬜ |
| FR-RCP-006 | Lưu trữ (Archive) | S | `PATCH /api/v1/recipes/{id}/archive` → 200 | S7 | `ArchiveRecipeCommand(+Handler)` | `recipe.Archive()` | `Recipes/ArchiveTests.cs` | — | ⬜ |
| FR-RCP-007 | Xóa công thức | M | `DELETE /api/v1/recipes/{id}` → 204 | S7 | `DeleteRecipeCommand(+Handler)` | **Soft delete** — không cascade, không xóa file MinIO | `Recipes/DeleteTests.cs` | **D1** | ⬜ |
| FR-RCP-008 | Quản lý ảnh | M | `POST /recipes/{id}/images` · `PATCH .../{imageId}` · `DELETE .../{imageId}` | S8 | `UploadRecipeImageCommand`, `UpdateRecipeImageCommand`, `DeleteRecipeImageCommand` | `Recipe.AttachImage/UpdateImage/RemoveImage`, `MinioFileStorageService`, magic-byte validation | `Recipes/ImagesTests.cs` (17 test, gồm hồi quy đổi primary qua lại + upload đồng thời + PATCH primary đồng thời) + 6 file `UnitTests/Images` + `UnitTests/Domain/RecipeImageTests.cs`. **FE:** `components/upload/*`, `lib/hooks/useRecipeImage{s,Gallery}.ts` — `__tests__/components/upload/*`, `__tests__/lib/**` (79 test Jest) | **D22, D27, D28, D30, D31**, D16 | ✅ |
| FR-RCP-009 | CRUD nguyên liệu | M | `POST/PUT/DELETE /recipes/{id}/ingredients/{ingId?}` | S6 | `Add/Update/DeleteIngredientCommand` | `RecipeIngredient.Create()` | `Recipes/IngredientsTests.cs` | **D7** | ⬜ |
| FR-RCP-010 | CRUD bước thực hiện | M | `POST/PUT/DELETE /recipes/{id}/steps/{stepId?}` | S6 | `Add/Update/DeleteStepCommand` | Auto-renumber `StepNumber` | `Recipes/StepsTests.cs` | **D6** | ⬜ |

**Điểm kiểm thử bắt buộc**

*Phân quyền*
- Guest chỉ thấy `Published` · Author thấy thêm Draft/Archived **của chính mình** · Admin thấy tất cả
- Author khác sửa recipe không phải của mình → 403 `RECIPE_FORBIDDEN`
- Admin sửa/xóa được recipe của Author khác (bypass ownership)

*Nghiệp vụ*
- Publish recipe thiếu step **hoặc** thiếu ingredient → **400 `RECIPE_PUBLISH_INCOMPLETE`** (D3)
- Publish 2 lần liên tiếp → idempotent, trả 200
- RowVersion mismatch → **409 `RECIPE_CONCURRENCY_CONFLICT`** (D4)
- Slug sinh tự động từ title, trùng thì auto-suffix — **không bao giờ trả 409** (D10)
- Slug **bất biến** sau khi tạo, kể cả khi Author đổi title (D13)
- `cookTime = 0` hợp lệ (món không cần nấu); `prepTime = 0` và `servings = 0` bị chặn (D19)
- `instructions` để trống được (cột NULL — D18)

*Xóa*
- `DELETE` → `IsDeleted = true`, Steps/Ingredients/Images **vẫn còn trong DB** (D1)
- File trên MinIO **không** bị xóa khi xóa recipe (D1)
- Slug của recipe đã xóa **không** dùng lại được → recipe mới cùng title nhận suffix (D1)

*Steps / Ingredients*
- Thêm 5 bước → `StepNumber` 1..5 (server sinh, client không gửi — D6)
- Xóa bước 3 → còn lại renumber thành 1..4
- `PUT` với `stepNumber` mới → chèn đúng vị trí, renumber toàn bộ
- Nguyên liệu "vừa đủ" (`quantity = null`, `unit = null`) lưu được (D7)

*Ảnh*
- Ảnh đầu tiên tự động `IsPrimary = true`, client không gửi (D22)
- `PATCH { isPrimary: true }` → ảnh khác về `false`
- `PATCH { isPrimary: false }` trên ảnh đang primary → **400** (D22)
- Xóa ảnh primary → ảnh có `orderIndex` nhỏ nhất lên thay

---

## FR-SRCH — Tìm kiếm & Phân trang (4 FR)

| FR | Tên | Ưu tiên | Endpoint / Tham số | Slice | Hiện thực | Test | Quyết định | TT |
|---|---|---|---|---|---|---|---|---|
| FR-SRCH-001 | Full-text search tiếng Việt | M | `GET /api/v1/recipes/search?q=` | S10 | `SearchRecipesQuery`, `SearchVector` tsvector + GIN index + trigger + `unaccent` | `Search/FullTextTests.cs` | D8, D10 | ⬜ |
| FR-SRCH-002 | Lọc | M | `?categoryId=&difficulty=&maxCookTime=&minServings=` | S4 | Tích hợp trong `GetRecipesQuery` (AND logic) | `Recipes/FilterTests.cs` | **D14** | ⬜ |
| FR-SRCH-003 | Sắp xếp | M | `?sort=createdAt` / `-createdAt` / `title` / `-cookTime` | S4 | Tích hợp trong `GetRecipesQuery`, mặc định `-createdAt` | `Recipes/SortTests.cs` | — | ⬜ |
| FR-SRCH-004 | Phân trang offset | M | `?page=1&pageSize=12` (max 50) | S4 | `PagedResult<T>` + COUNT trước SKIP/TAKE | `Recipes/PaginationTests.cs` | — | ⬜ |

**Điểm kiểm thử bắt buộc**

- `q` < 2 ký tự → **400 `VALIDATION_ERROR`** (D4)
- Tìm "pho" khớp "Phở bò"; tìm "bo" khớp "Bò kho" (unaccent)
- Kết quả xếp theo `ts_rank` giảm dần
- Chỉ trả `Published` — không lọt Draft của bất kỳ ai
- `pageSize = 100` → **clamp về 50**, không trả lỗi
- Ký tự đặc biệt trong `q` (`'`, `&`, `!`, `:`) không làm sập tsquery
- Đủ 4 filter, kết hợp AND, gồm cả `minServings` (D14)
- Route `/recipes/search` đăng ký **trước** `/recipes/{slug}` — request tới `/recipes/search`
  không rơi vào route slug (D10)

---

## FR-FILE — Quản lý Tệp tin (2 FR)

| FR | Tên | Gọi từ đâu | Slice | Hiện thực | Test | Quyết định | TT |
|---|---|---|---|---|---|---|---|
| FR-FILE-001 | Upload file lên MinIO | FR-RCP-008 | S8 | `IFileStorageService.UploadAsync()` → `MinioFileStorageService` (AWSSDK.S3). Bucket `culinary-blog` (public-read), key `recipes/{recipeId}/{Guid}{ext}` (`ObjectKey`), max 5MB, magic bytes (`ImageSignature`) | `UnitTests/Images/{ImageSignatureTests,ObjectKeyTests}.cs`. **`MinioFileStorageService` chạy thật (S3 client) chưa có test riêng** — `ImagesTests.cs` dùng `FakeFileStorageService`, chỉ xác nhận Command/Handler/Domain/endpoint | D16 | ✅* |
| FR-FILE-002 | Xóa file khỏi MinIO | **Chỉ** từ `DELETE /recipes/{id}/images/{imageId}` | S8 | `IFileStorageService.DeleteAsync()`. Idempotent (bắt `AmazonS3Exception` 404). Enqueue qua `IBackgroundJobService.EnqueueDeleteImageFile()` — Hangfire retry 3× mặc định | Như trên (`ImagesTests.cs` xác nhận enqueue qua Fake, chưa test job Hangfire chạy thật) | **D1** | ✅* |

> **D1 thu hẹp phạm vi FR-FILE-002:** vì Recipe là soft delete, không có job xóa hàng loạt
> file khi xóa recipe. FR-FILE-002 chỉ chạy khi Author xóa **một ảnh cụ thể**.
>
> **\* Còn thiếu:** test tích hợp `MinioFileStorageService` với MinIO thật (Testcontainers hoặc
> docker-compose) — việc gọi S3 API thật (`PutObjectAsync`/`DeleteObjectAsync`) chưa được xác
> nhận bằng test tự động, chỉ được review bằng mắt. Cần làm trước khi coi FR-FILE-001/002 "xong"
> theo đúng nghĩa `docs/CLAUDE.md` mục 7 (integration test mỗi endpoint).

---

## FR-JOB — Background Jobs (3 FR)

| FR | Job | Loại | Trigger | Slice | Hiện thực | Test | Quyết định | TT |
|---|---|---|---|---|---|---|---|---|
| FR-JOB-001 | Welcome Email | Fire-and-forget | Sau FR-AUTH-001 | S9 | `WelcomeEmailJob`, `MailKitEmailService`. Retry 3× (1p/5p/30p) | `Jobs/WelcomeEmailTests.cs` | **D12** | ⬜ |
| FR-JOB-002 | Image Resize / Thumbnail | Fire-and-forget | Sau FR-RCP-008 upload | S8 | **Cố ý CHƯA làm** — xem ghi chú dưới | `Jobs/ImageResizeTests.cs` | — | ⬜ |
| FR-JOB-003 | Sitemap Generation | Recurring | Cron `0 2 * * *` (02:00 UTC) | S11 | `SitemapGenerationJob` → sitemap.xml (Published recipes + categories + trang tĩnh), ping Google. Retry 2× | `Jobs/SitemapTests.cs` | — | ⬜ |

> **FR-JOB-002 cố ý để lại (2026-09-22):** roadmap gộp FR-JOB-002 vào S8 cùng FR-RCP-008, nhưng
> chưa có test nào cho nó (khác FR-RCP-008/FR-FILE-001/002 đã có 14+ test tích hợp và unit
> pass). Theo quy tắc "Test first" (CLAUDE.md mục 9), không code phần resize/thumbnail (cần
> S3 client + ImageSharp + cập nhật DB ngoài luồng MediatR) khi chưa có test dẫn dắt, để tránh
> giao logic ảnh xử lý bất đồng bộ chưa qua CI thật. Làm ở slice riêng, viết integration test
> trước.

> **D12:** email chào mừng **không có link kích hoạt** — chỉ chào mừng + link về trang chủ
> và `/dashboard`.

**Hangfire Dashboard** tại `/hangfire`, chỉ Admin (policy-protected), storage PostgreSQL.

---

## FR-OBS — Quan sát Hệ thống (3 FR)

| FR | Tên | Endpoint / Cơ chế | Slice | Hiện thực | Test | TT |
|---|---|---|---|---|---|---|
| FR-OBS-001 | Health Check | `GET /health` · `/health/live` · `/health/ready` | S1 | `AspNetCore.HealthChecks.NpgSql` + `.Redis` + `.Minio`. Live = process; Ready = DB + Redis | `Observability/HealthTests.cs` | ⬜ |
| FR-OBS-002 | Structured Logging | Serilog + `CorrelationIdMiddleware` | S1 | Mọi request log `CorrelationId` (X-Correlation-ID), method/path/status/elapsed/UserId. `LoggingBehavior` log mọi Command/Query. Cảnh báo > 500ms | `Observability/LoggingTests.cs` | ⬜ |
| FR-OBS-003 | Tracing & Metrics | OpenTelemetry → OTLP | S11 | HTTP traces, EF Core traces, custom metrics (recipe created/published). `Activity.TraceId` gắn vào structured log | `Observability/TracingTests.cs` | ⬜ |

---

## NFR — Yêu cầu Phi chức năng (29 mục, kiểm tra ở cuối mỗi sprint)

| Nhóm | Mã | Ngưỡng cần đạt | Cách đo | TT |
|---|---|---|---|---|
| PERF | NFR-PERF-001 | p50 ≤ 150ms (GET cached), p95 ≤ 500ms, p99 ≤ 1000ms | k6 + OpenTelemetry | ⬜ |
| PERF | NFR-PERF-002 | ≥ 100 concurrent users trên 2 vCPU / 4GB | k6 smoke → load → stress | ⬜ |
| PERF | NFR-PERF-003 | Cache hit rate ≥ 80%. TTL theo D8 | Redis INFO stats | ⬜ |
| PERF | NFR-PERF-004 | Không N+1; mọi WHERE/ORDER BY có index; slow query > 100ms cảnh báo | EXPLAIN ANALYZE review trước merge | ⬜ |
| PERF | NFR-PERF-005 | LCP ≤ 2.5s · CLS ≤ 0.1 · INP ≤ 200ms · JS bundle ≤ 200KB gzip | Lighthouse CI | ⬜ |
| SEC | NFR-SEC-001 | PBKDF2-HMACSHA512, ≥ 100.000 iterations. Password ≥ 8 ký tự: hoa + **thường** + số + đặc biệt | Cấu hình `IdentityOptions` | ⬜ |
| SEC | NFR-SEC-002 | AT 15p HS256; RT 128-bit random, SHA-256 hash, 7 ngày, rotation + reuse detection (D20) | Integration test | ⬜ |
| SEC | NFR-SEC-003 | `/auth/*` 10 req/p/IP · API chung 100 · upload 5 · 429 + Retry-After (D15) | ASP.NET Rate Limiting middleware | ⬜ |
| SEC | NFR-SEC-004 | FluentValidation trước xử lý; magic bytes; check size trước khi read stream; filename GUID | Security test | ⬜ |
| SEC | NFR-SEC-005 | HTTPS TLS 1.2+, HSTS, CORS allowlist (không `*`), cookie SameSite=Strict | Nginx + appsettings | ⬜ |
| SEC | NFR-SEC-006 | Authorization ở Application Layer; chỉ 2 policy `AuthorPolicy`/`AdminPolicy` (D12); audit log mọi write | ArchUnit + code review | ⬜ |
| SEC | NFR-SEC-007 | Không secret trong Git; pre-commit hook trufflehog/gitleaks | CI | ⬜ |
| USE | NFR-USE-001 | Breakpoint 320/768/1200px, Tailwind utility-first | Chrome DevTools + BrowserStack | ⬜ |
| USE | NFR-USE-002 | WCAG 2.1 AA, contrast ≥ 4.5:1, keyboard nav đầy đủ | axe + NVDA/VoiceOver | ⬜ |
| USE | NFR-USE-003 | RFC 7807 + inline field error + error code i18n-ready (bảng cuối `decisions.md`) | Code review | ⬜ |
| USE | NFR-USE-004 | Skeleton, optimistic update + rollback, toast, upload progress bar | Manual + E2E | ⬜ |
| REL | NFR-REL-001 | Uptime ≥ 99.5%, readiness probe mỗi 10s | Uptime Robot | ⬜ |
| REL | NFR-REL-002 | GlobalExceptionMiddleware; **Redis down → đi thẳng xuống handler, không throw** (D8); Hangfire retry 3× | Chaos test | ⬜ |
| REL | NFR-REL-003 | WAL, pg_dump hàng ngày 03:00 (giữ 30 ngày), MinIO volume persistent, soft delete (D1) | Ops | ⬜ |
| MAINT | NFR-MAINT-001 | 0 compiler warning, SonarAnalyzer/StyleCop, ESLint + Prettier, ≥1 reviewer | CI gate | ⬜ |
| MAINT | NFR-MAINT-002 | Unit ≥ 80% Application; mỗi endpoint ≥ 1 happy + 1 error; 5 E2E flow | Coverage report | ⬜ |
| MAINT | NFR-MAINT-003 | README setup < 5 phút, Scalar UI `/scalar`, ADR, CHANGELOG SemVer | Review | ⬜ |
| MAINT | NFR-MAINT-004 | Dependency Rule; CQRS không trộn read/write | ArchUnit.NET | ⬜ |
| SCALE | NFR-SCALE-001 | Stateless; **chỉ Redis, không IMemoryCache** (D8); RedLock; Hangfire multi-worker | Review | ⬜ |
| SCALE | NFR-SCALE-002 | Npgsql pool max 100; B-tree + GIN index | Review | ⬜ |
| SCALE | NFR-SCALE-003 | Mỗi service 1 container; Nginx upstream pool; CDN static assets | Docker Compose | ⬜ |
| SEO | NFR-SEO-001 | JSON-LD Schema.org Recipe, pass 100% Google Rich Results Test | Rich Results Test | ⬜ |
| SEO | NFR-SEO-002 | `<title>` ≤ 60 ký tự, meta description 150–160, OG image 1200×630, canonical, noindex cho draft/archived | Lighthouse SEO | ⬜ |
| SEO | NFR-SEO-003 | sitemap.xml tự sinh hàng ngày + robots.txt + ping GSC | FR-JOB-003 | ⬜ |
| SEO | NFR-SEO-004 | `/recipes/{slug}` không dấu; **slug bất biến sau khi tạo; không có 301 redirect** (D13) | Manual | ⬜ |

---

## Bảng đếm nhanh

| Module | Tổng FR | ✅ | 🟡 | ⬜ |
|---|---|---|---|---|
| FR-AUTH | 7 | 0 | 0 | 7 |
| FR-CAT | 5 | 0 | 0 | 5 |
| FR-RCP | 10 | 0 | 0 | 10 |
| FR-SRCH | 4 | 0 | 0 | 4 |
| FR-FILE | 2 | 0 | 0 | 2 |
| FR-JOB | 3 | 0 | 0 | 3 |
| FR-OBS | 3 | 0 | 0 | 3 |
| **Tổng** | **34** | **0** | **0** | **34** |

> Không còn FR nào bị chặn — cả 22 mâu thuẫn trong SRS đã chốt tại `decisions.md`.
> Cột **Quyết định** ở mỗi bảng cho biết FR đó phải đọc mục D nào trước khi code.
