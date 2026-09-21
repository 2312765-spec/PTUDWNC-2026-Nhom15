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
| `AUTH_REFRESH_TOKEN_EXPIRED` | 401 | Auth | — |
| `AUTH_REFRESH_TOKEN_REVOKED` | 401 | Auth | — |
| `AUTH_GOOGLE_TOKEN_INVALID` | 400 | Auth | — |
| `AUTH_ACCOUNT_DISABLED` | 403 | Auth | — |
| `AUTH_ACCOUNT_LOCKED` | **423** | Auth | 🆕 **thêm mới** (D17) |
| `RECIPE_NOT_FOUND` | 404 | Recipe | — |
| ~~`RECIPE_SLUG_EXISTS`~~ | — | — | ❌ **bỏ** (D10) |
| `RECIPE_PUBLISH_INCOMPLETE` | 400 | Recipe | — |
| `RECIPE_FORBIDDEN` | 403 | Recipe | — |
| `RECIPE_CONCURRENCY_CONFLICT` | **409** | Recipe | ✏️ đổi từ 422 (D4) |
| `RECIPE_IMAGE_NOT_FOUND` | 404 | Recipe | 🆕 **thêm mới** — SRS FR-RCP-008 chỉ ghi chung "404", chưa có mã riêng cho ảnh không tồn tại (phát hiện lúc lập kế hoạch FR-RCP-008) |
| `RECIPE_PRIMARY_IMAGE_REQUIRED` | 400 | Recipe | 🆕 **thêm mới** (D22) — `PATCH` `{ isPrimary: false }` trên ảnh đang là primary bị từ chối, vì recipe phải luôn có đúng 1 ảnh primary nếu còn ảnh |
| `CATEGORY_NOT_FOUND` | 404 | Category | — |
| `CATEGORY_NAME_EXISTS` | 409 | Category | — |
| `CATEGORY_DELETE_HAS_RECIPES` | 409 | Category | — |
| `FILE_SIZE_EXCEEDED` | 400 | File | — |
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

## Khi phát hiện mâu thuẫn mới lúc code

1. Thêm mục mới vào cuối file này với mã `D23`, `D24`…
2. Ghi: nguồn mâu thuẫn (trích trang SRS) → quyết định → lý do → cái gì phải sửa trong SRS.
3. Cập nhật `traceability.md` nếu nó ảnh hưởng FR nào.
4. Nếu là quyết định **kiến trúc** (không phải làm rõ SRS) thì viết ADR trong `docs/adr/`.
