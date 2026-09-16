# Lộ trình Implement — Culinary Blog v1.0.0

34 FR chia thành **12 slice dọc**. Mỗi slice đi trọn từ database lên UI và kết thúc
bằng một thứ **chạy được và demo được** — không chia theo tầng ("tuần này làm hết
database, tuần sau làm hết API"), vì cách đó không cho ai thấy tiến độ thật cho đến cuối.

Mỗi slice ghi rõ **quyết định D-x** phải đọc trước khi code. Chúng nằm ở `docs/decisions.md`
và **thắng SRS** ở mọi chỗ mâu thuẫn.

---

## Bản đồ phụ thuộc

```
S0  Chuẩn bị tài liệu
     │
S1  Khung + Hạ tầng + Observability
     │
     ├── S2  Auth (đăng ký/đăng nhập/token)
     │        │
     │   S3  Categories (CRUD, Admin) ──┐
     │        │                          │
     │   S4  Recipe – ĐỌC (list/detail/filter/sort/paging)
     │        │
     │   S5  Recipe – VIẾT (create/update)
     │        │
     │   S6  Steps + Ingredients
     │        │
     │   S7  Vòng đời (publish/unpublish/archive/delete)
     │        │
     │   S8  Ảnh (MinIO + upload + resize job)
     │        │
     ├── S9  Google OAuth + Welcome email        (song song được từ sau S2)
     ├── S10 Full-text search tiếng Việt          (song song được từ sau S4)
     ├── S11 SEO + Sitemap + Tracing              (song song được từ sau S7)
     └── S12 Hardening: rate limit, perf, a11y, E2E   ← cuối cùng
```

**Ước lượng:** S1–S8 là đường găng (critical path). S9, S10, S11 chạy song song được
nếu có nhiều người. S12 phải ở cuối.

---

## S0 — Chuẩn bị tài liệu

**Không viết code.** Hai việc:

1. **Chuyển SRS sang markdown.** `docs/SRS.md`, tách theo chương, giữ nguyên bảng, đặt
   neo theo mã FR. PDF 71 trang phải đọc dưới dạng ảnh — rất tốn context và không grep được.
   Có markdown thì mỗi lần chỉ nạp đúng mục FR đang làm.
2. **Đọc hết `docs/decisions.md` một lượt.** 22 quyết định, khoảng 10 phút. Không đọc thì
   sẽ code theo SRS ở những chỗ SRS sai — tốn công sửa lại về sau.

**Xong khi:** `docs/SRS.md` tồn tại, mục lục có neo tới từng mã FR; bạn trả lời được
bốn câu: xóa recipe là hard hay soft? validation trả mã gì? cache dùng gì? trường hồ sơ
tên là gì?

---

## S1 — Khung dự án + Hạ tầng + Observability

**FR phủ:** FR-OBS-001, FR-OBS-002 · **Phụ thuộc:** S0 · **Quyết định:** D4 (map exception)

**Nội dung:**
- Solution 4 project theo CONS-001 + 3 project test.
- `docker-compose.yml`: postgres 16 (+ extension `unaccent`, `pg_trgm`), redis 7, minio,
  seq, mailhog.
- `CulinaryBlogDbContext` + `BaseEntity` + `AuditInterceptor` + **Global Query Filter
  `IsDeleted`** (nền tảng của D1/D2 — làm đúng ngay từ đầu) + migration đầu tiên.
- MediatR + 5 pipeline behavior (khung, chưa cần đầy đủ logic).
- `GlobalExceptionMiddleware` → RFC 7807 với **bảng map exception → HTTP của D4**.
  Viết đúng ngay bây giờ thì 11 slice sau không phải đụng lại.
- `CorrelationIdMiddleware`, Serilog → Console + Seq, health checks 3 endpoint.
- Next.js App Router + Tailwind + layout rỗng. Nginx config.
- **ArchUnit test kiểm tra Dependency Rule** — viết ngay từ đầu, không để cuối.

**Xong khi:** `docker compose up` → `GET /health` trả 200 với cả 3 component healthy ·
`/scalar` mở được · `localhost:3000` render layout · `dotnet test` pass · 0 compiler
warning · architecture test pass · ném thử `ValidationException` ra endpoint tạm → nhận
đúng 400 `VALIDATION_ERROR` dạng RFC 7807.

> **Prompt:**
> ```
> Đọc CLAUDE.md, docs/decisions.md (D4), và docs/SRS.md mục 2.4, 2.5 (ràng buộc CONS),
> 6.2 (Clean Architecture), 6.5 (Docker Compose), FR-OBS-001/002.
>
> Lập kế hoạch dựng khung dự án: liệt kê từng project, package NuGet, file cấu hình,
> và bảng map exception → HTTP status trong GlobalExceptionMiddleware theo D4.
> Chưa viết code — tôi duyệt kế hoạch trước.
> ```

---

## S2 — Xác thực & Tài khoản

**FR phủ:** FR-AUTH-001, 002, 004, 005, 006, 007 (6 FR) · **Phụ thuộc:** S1
**Quyết định:** **D5** (tên trường), **D20** (schema RefreshToken), D4, D11, D17

**Nội dung:**
- `ApplicationUser : IdentityUser<string>` với `DisplayName`, `AvatarUrl`, `Bio`, `IsActive`
  + `RefreshToken` entity **theo đúng schema 7.8** (D20: có `TokenHash`, `RevokedAt`,
  `ReplacedByTokenHash`; **không có cột `IsRevoked`**) + migration.
- Seed role `Author`, `Admin` + 1 tài khoản Admin.
- `JwtService`: access token HS256 15 phút (claims: userId, email, roles, jti);
  refresh token 128-bit random, **lưu SHA-256 hash**, 7 ngày.
- 6 command/query + validator. **Token rotation + reuse detection**: nhận token có
  `RevokedAt != null` → revoke cả token family + log WARNING + 401.
- Kiểm tra `IsActive == false` → 403 `AUTH_ACCOUNT_DISABLED` ở cả login và refresh (D11).
- Lockout 5 lần / 15 phút → **423 `AUTH_ACCOUNT_LOCKED`** (D17).
- Frontend: `/auth/login`, `/auth/register`, `/profile`, Auth.js v5 session,
  axios interceptor tự refresh khi 401.

**Xong khi:** đăng ký → nhận token · đăng nhập → nhận token · access token hết hạn →
auto refresh · logout → refresh token bị revoke · dùng lại refresh token đã revoke →
401 + cả family bị revoke · sai mật khẩu 5 lần → 423 · `IsActive = false` → 403 ·
validation fail → 400 (không phải 422) · integration test đủ happy + error case cho cả 6 endpoint.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D5, D20, D4, D11, D17.
> Đọc docs/SRS.md mục 3.1 (FR-AUTH-001, 002, 004, 005, 006, 007), 7.7, 7.8, 8.1,
> NFR-SEC-001/002.
>
> Lưu ý: SRS Chương 3 dùng fullName/userName và trả 422 — cả hai đều SAI theo D5 và D4.
> Dùng displayName/avatarUrl/bio và trả 400.
> RefreshToken theo schema 7.8: không có cột IsRevoked, nó là computed từ RevokedAt.
>
> Bước 1: viết integration test cho FR-AUTH-001, 002, 004, 005 từ SRS + decisions.
> Mỗi test ghi mã FR trong DisplayName. Chạy, xác nhận fail. Chưa implement.
> ```

---

## S3 — Quản lý Danh mục

**FR phủ:** FR-CAT-001 → 005 (5 FR) · **Phụ thuộc:** S2 (cần role Admin)
**Quyết định:** **D8** (cache), **D2** (soft delete), D10 (slug)

**Nội dung:**
- Entity `Category` + migration.
- `SlugHelper.Generate()` — bỏ dấu tiếng Việt, lowercase, thay ký tự lạ bằng `-`,
  **auto-suffix `-2`, `-3`** khi trùng, **cấm slug `search`/`new`/`edit`** (D10).
  Đây là thành phần dùng lại cho Recipe ở S5 — thiết kế cho cả hai.
- 5 command/query. `DeleteCategoryCommand` đặt `IsDeleted = true`, đếm recipe **chưa xóa**
  > 0 → 409 (D2).
- **`CachingBehavior` + `CacheInvalidationBehavior` thật sự chạy.** Đây là slice đầu dùng
  cache — hiện thực đúng D8 tại đây thì S4 chỉ việc gắn `ICacheable` vào query.
  Redis, tag-based, `categories:all` TTL 30 phút. Command trên Category xóa **cả hai tag**
  `categories` và `recipes`.
- Frontend `/categories`, `/categories/[slug]`, `/dashboard/categories`.

**Xong khi:** Admin CRUD được · Author POST → 403 · Guest → 401 · xóa category còn recipe
→ 409 · xóa thành công → `IsDeleted = true`, bản ghi còn trong DB · "Món khai vị" →
`mon-khai-vi` · slug trùng → `-2` · sửa tên category rồi `GET /recipes` ngay → thấy tên mới.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D8 (toàn bộ, gồm bảng TTL và quy tắc invalidation), D2, D10.
> Đọc docs/SRS.md mục 3.2 (FR-CAT-001..005), 7.6, 8.2, và docs/permissions.md mục 3.3.
>
> SRS FR-CAT-001 nói dùng IMemoryCache — SAI theo D8. Chỉ dùng Redis qua CachingBehavior.
> SRS FR-CAT-005 nói "xóa entity" — SAI theo D2. Soft delete.
>
> Lập kế hoạch. SlugHelper phải xử lý tiếng Việt có dấu và dùng lại được cho Recipe.
> Chưa code.
> ```

---

## S4 — Recipe: luồng ĐỌC

**FR phủ:** FR-RCP-001, 002 + FR-SRCH-002, 003, 004 (5 FR) · **Phụ thuộc:** S3
**Quyết định:** D8 (cache), D14 (`minServings`), D18 (`Instructions` NULL)

**Nội dung:**
- Entity `Recipe` + `RecipeNutrition` (owned) + `RecipeStep`, `RecipeIngredient`,
  `RecipeImage` + **toàn bộ migration**, với `Instructions` là **NULL** (D18).
- Index theo mô hình 7.2. Seed 50 recipe / 5 author bằng Bogus — **seed cả Draft và
  Archived**, nếu toàn Published thì bug phân quyền không lộ ra.
- `GetRecipesQuery` với authorization filter + **4 filter gồm `minServings`** (D14)
  + sort + `PagedResult<T>`, `pageSize` clamp về 50.
- `GetRecipeBySlugQuery` với eager loading.
- Cache: `recipes:list:{hash}` 15 phút, `recipe:{slug}` 5 phút, tag `recipes` (D8).
- Frontend `/`, `/recipes`, `/recipes/[slug]` với ISR đúng revalidate.

> Làm hết schema ở đây, kể cả bảng cho S5–S8 — một migration lớn sạch hơn năm migration vá.

**Xong khi:** Guest chỉ thấy Published · Author thấy thêm Draft **của mình** · Admin thấy
tất cả · truy cập Draft của người khác theo slug → 403 · 4 filter + sort + paging đúng ·
`pageSize=100` → clamp 50 · **không có N+1** (kiểm bằng log EF Core) · p95 < 500ms trên
50 recipe seed.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D8, D14, D18.
> Đọc docs/SRS.md mục 3.3 (FR-RCP-001, 002), 3.4 (FR-SRCH-002/003/004), toàn bộ chương 7,
> mục 8.3, và docs/permissions.md mục 3.1.
>
> SRS FR-RCP-001/002 dùng Output Cache — SAI theo D8. Dùng CachingBehavior + Redis.
> Cột Instructions là NULL theo D18, không phải NOT NULL.
>
> Lập kế hoạch tạo TOÀN BỘ entity + migration (cả bảng dùng ở slice sau), rồi implement
> 2 query đọc. Chú ý authorization filter ở bước 4 của FR-RCP-001 — logic dễ sai nhất
> slice này. Seed data phải có cả Draft và Archived. Chưa code.
> ```

---

## S5 — Recipe: TẠO và SỬA

**FR phủ:** FR-RCP-003, 004 (2 FR) · **Phụ thuộc:** S4
**Quyết định:** **D10** (slug auto-suffix + bất biến), **D4** (409), **D19** (`cookTime ≥ 0`), D13

**Nội dung:**
- `Recipe.Create()` factory + `recipe.Update()` domain method (không public setter).
- Validator: `title` 5–200 · `description` 1–2000 · **`prepTime > 0`, `cookTime >= 0`,
  `servings > 0`** (D19 — SRS viết `> 0` cho cả ba, chặn oan món nguội) · `instructions`
  optional.
- Slug sinh từ title lúc tạo, auto-suffix khi trùng, **bất biến sau đó** kể cả khi đổi
  title (D10 + D13).
- **`RecipeAuthorizationHandler`** — resource-based auth. Mảnh quan trọng nhất slice này.
- Optimistic concurrency qua `RowVersion` + `If-Match` header → mismatch trả
  **409 `RECIPE_CONCURRENCY_CONFLICT`** (D4 — Phụ lục A/B của SRS ghi 422, sai).
- Frontend `/dashboard/recipes`, `/dashboard/recipes/new` (multi-step wizard),
  `/dashboard/recipes/[id]/edit`.

**Xong khi:** Author tạo được recipe (Status = Draft) · Author khác sửa → 403 · Admin sửa
được của người khác · hai request sửa cùng lúc → request thứ hai nhận **409** · tạo 2
recipe cùng title → slug thứ hai có `-2` · đổi title → slug **không đổi** · `cookTime = 0`
lưu được.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D10, D4, D19, D13.
> Đọc docs/SRS.md FR-RCP-003, FR-RCP-004, mục 7.2, NFR-SEC-006.
>
> SRS FR-RCP-003 A3 trả 409 khi slug trùng — SAI theo D10, phải auto-suffix.
> SRS bước 4 validate cookTime > 0 — SAI theo D19, phải >= 0.
> Concurrency conflict trả 409 theo D4 (Phụ lục A/B ghi 422, sai).
>
> Viết test trước cho: tạo thành công, Author khác sửa → 403, Admin bypass được,
> RowVersion mismatch → 409, 2 recipe cùng title → slug có suffix, đổi title → slug
> giữ nguyên. Chạy, xác nhận fail.
> ```

---

## S6 — Bước thực hiện & Nguyên liệu

**FR phủ:** FR-RCP-009, 010 (2 FR) · **Phụ thuộc:** S5
**Quyết định:** **D6** (RecipeStep), **D7** (RecipeIngredient)

**Nội dung:**

*Steps (D6)* — `title` bắt buộc ≤ 200, `description` ≤ 2000, `timerMinutes` optional
(**không phải** `durationMinutes`). `POST` **không nhận** `stepNumber` — server đặt
`Max + 1`. `PUT` nhận `stepNumber?` để đổi vị trí → renumber toàn bộ. `DELETE` → renumber
lại cho liên tục. **Logic renumber là chỗ dễ sai nhất slice này.**

*Ingredients (D7)* — `name` 1–**200**, `quantity` **nullable** (có thì phải `> 0`),
`unit` **nullable**, `notes` ≤ 500, `orderIndex` (**không phải** `sortOrder`).
Nullable là có chủ đích: "muối vừa đủ", "tiêu tùy khẩu vị".

Cả hai kế thừa authorization từ Recipe cha. Frontend: form động thêm/xóa/kéo-thả trong wizard.

**Xong khi:** thêm 5 bước → `StepNumber` 1..5 · xóa bước 3 → còn lại 1..4 ·
`PUT` với `stepNumber = 2` → chèn đúng chỗ, renumber toàn bộ · nguyên liệu
`quantity = null, unit = null` lưu được · `quantity = 0` bị chặn · Author khác thao tác → 403.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D6 và D7 (cả hai bảng trường).
> Đọc docs/SRS.md FR-RCP-009, FR-RCP-010, mục 7.3, 7.4, 8.5, 8.6.
>
> Theo D6: tên trường là timerMinutes (không phải durationMinutes), title bắt buộc,
> POST không nhận stepNumber (server sinh).
> Theo D7: quantity và unit NULLABLE (SRS FR-RCP-009 bắt buộc > 0 — sai),
> tên trường orderIndex (không phải sortOrder), name tối đa 200.
>
> Chú ý đặc biệt: logic renumber StepNumber. Viết unit test cho domain logic này trước
> (xóa đầu / giữa / cuối / chèn vào giữa), rồi mới làm endpoint.
> ```

---

## S7 — Vòng đời Recipe

**FR phủ:** FR-RCP-005, 006, 007 (3 FR) · **Phụ thuộc:** S6
**Quyết định:** **D1** (soft delete), **D3** (điều kiện publish), D4

**Nội dung:**
- `recipe.Publish()` — ném `DomainException` nếu `Steps.Count == 0` **hoặc**
  `Ingredients.Count == 0` (D3 — SRS FR-RCP-005 chỉ nói step, thiếu ingredient).
  Lỗi trả **400 `RECIPE_PUBLISH_INCOMPLETE`**, message nói rõ thiếu cái nào.
- `recipe.Unpublish()`, `recipe.Archive()`. Set `PublishedAt` khi publish. Idempotent.
- `DeleteRecipeCommand` → **`IsDeleted = true`**. **Không cascade. Không Hangfire job xóa
  file MinIO** (D1 — SRS FR-RCP-007 nói hard delete + cascade + xóa MinIO, sai cả ba).
- Cache invalidation theo tag `recipes` + `recipe:{slug}` sau mỗi thao tác (D8).
- Frontend: nút publish/archive/delete + confirm dialog.

**Xong khi:** publish recipe thiếu step → 400 · thiếu ingredient → 400 · publish 2 lần →
idempotent 200 · archive → biến mất khỏi listing công khai nhưng còn trong DB ·
delete → `IsDeleted = true`, Steps/Ingredients/Images **vẫn còn trong DB**, file MinIO
**vẫn còn** · slug của recipe đã xóa không dùng lại được · cache bị xóa sau mỗi thao tác.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D1 (đọc kỹ phần "Hệ quả cụ thể"), D3, D4.
> Đọc docs/SRS.md FR-RCP-005, 006, 007.
>
> CẢNH BÁO: SRS FR-RCP-007 nói "hard delete, cascade delete, Hangfire xóa file MinIO".
> Cả ba đều SAI theo D1. Đây là soft delete: chỉ đặt IsDeleted = true, không cascade,
> không đụng MinIO.
> Theo D3: publish cần >= 1 step VÀ >= 1 ingredient (SRS chỉ nói step).
>
> Business rule nằm trong Domain method, không nằm trong handler. Viết unit test cho
> Recipe.Publish() trước (không cần DB), rồi mới làm command handler.
> ```

---

## S8 — Ảnh công thức

**FR phủ:** FR-RCP-008, FR-FILE-001, FR-FILE-002, FR-JOB-002 (4 FR) · **Phụ thuộc:** S7
**Quyết định:** **D22** (endpoint PATCH chung), D16 (public-read), D1

**Nội dung:**
- `IFileStorageService` (Application) + `MinioFileStorageService` (Infrastructure, AWSSDK.S3).
- Validation nhiều lớp theo đúng thứ tự: **size trước khi đọc stream** → MIME →
  **magic bytes** (JPEG `FF D8 FF`, PNG `89 50 4E 47`). Filename `recipes/{recipeId}/{Guid}{ext}`.
- **Ba endpoint theo D22:** `POST /images` (body: `file`, `altText?`) ·
  `PATCH /images/{imageId}` (body: `altText?`, `isPrimary?`, `orderIndex?`) ·
  `DELETE /images/{imageId}`. **Không có** `/images/{imageId}/primary` như SRS viết.
- Logic `IsPrimary`: ảnh đầu tiên tự động primary (server quyết, client không gửi) ·
  `PATCH { isPrimary: true }` → ảnh khác về false · `PATCH { isPrimary: false }` trên ảnh
  đang primary → **400** · `DELETE` ảnh primary → ảnh `orderIndex` nhỏ nhất lên thay.
- Hangfire `ImageResizeJob` → medium 800×600 + thumbnail 300×300.
- FR-FILE-002 **chỉ chạy khi xóa một ảnh cụ thể**, không chạy khi xóa recipe (D1).
- Frontend upload có progress bar realtime.
- Viết `docs/adr/0001-minio-public-read.md` (D16).

**Xong khi:** upload 6MB → 400 `FILE_SIZE_EXCEEDED` · upload .exe đổi tên thành .jpg →
400 `FILE_MIME_INVALID` (magic bytes bắt được) · tên file chứa `../` không thoát được
thư mục · ảnh đầu tiên tự động primary · job resize xong thì `MediumUrl`/`ThumbnailUrl`
được cập nhật · xóa recipe → file trên MinIO **vẫn còn** (D1).

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D22, D16, D1.
> Đọc docs/SRS.md FR-RCP-008, mục 3.5 (FR-FILE), FR-JOB-002, CONS-007, NFR-SEC-004,
> mục 7.5, 8.4.
>
> Theo D22: BA endpoint (POST /images, PATCH /images/{imageId}, DELETE /images/{imageId}).
> KHÔNG làm endpoint /images/{imageId}/primary như SRS FR-RCP-008 viết.
> Theo D1: xóa recipe KHÔNG xóa file MinIO. FR-FILE-002 chỉ dùng cho DELETE một ảnh.
>
> Ưu tiên bảo mật upload: viết test cho 4 trường hợp tấn công trước — file quá lớn,
> MIME sai, magic bytes không khớp extension, tên file chứa "../". Chạy, xác nhận fail.
> ```

---

## S9 — Google OAuth + Email chào mừng

**FR phủ:** FR-AUTH-003, FR-JOB-001 (2 FR) · **Phụ thuộc:** S2 (song song được với S4–S8)
**Quyết định:** **D9** (ID Token), **D12** (không xác nhận email), D5

**Nội dung:**
- `POST /api/v1/auth/google` body **`{ idToken }`** (D9). Backend verify ID Token với
  Google, lấy `email`, `name`, `picture`, `sub`. **Không có** endpoint `/auth/google/callback`.
- Frontend: Auth.js v5 Google provider để lấy ID Token.
- **Liên kết tài khoản trùng email:** đã có user với email đó → `AddLoginAsync` vào tài
  khoản hiện có, **không tạo tài khoản thứ hai**. Chưa có → tạo mới, `displayName` =
  Google `name`, `avatarUrl` = Google `picture`, gán role `Author` (D5, D9).
- `IEmailService` + `MailKitEmailService` → Mailhog ở dev.
- Hangfire `WelcomeEmailJob` fire-and-forget, retry 3× (1p/5p/30p). Email **không có link
  kích hoạt** — chỉ chào mừng + link trang chủ + link `/dashboard` (D12).

**Xong khi:** đăng nhập Google lần đầu → tạo tài khoản, role Author · email Google trùng
tài khoản đã đăng ký thủ công → **liên kết**, tổng số user không tăng · đăng ký → email
chào mừng xuất hiện trong Mailhog · Google API lỗi → 502, không crash.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D9, D12, D5.
> Đọc docs/SRS.md FR-AUTH-003, FR-JOB-001, mục 5.3.
>
> Theo D9: body là { idToken }, backend verify với Google. SRS FR-AUTH-003 nói
> ExternalLoginInfo và mục 5.3 nói Authorization Code + PKCE với callback endpoint —
> cả hai đều KHÔNG dùng.
> Theo D12: email chào mừng KHÔNG có link kích hoạt. Policy VerifiedAuthor đã bỏ.
>
> Chú ý trường hợp khó nhất: user đã đăng ký bằng email X, sau đó đăng nhập Google
> cũng với email X. Phải LIÊN KẾT (AddLoginAsync), không tạo tài khoản thứ hai.
> Viết test cho tình huống đó trước.
> ```

---

## S10 — Full-text search tiếng Việt

**FR phủ:** FR-SRCH-001 (1 FR) · **Phụ thuộc:** S4 (song song được với S5–S8)
**Quyết định:** D8 (TTL 1 phút), D10 (cấm slug `search`)

**Nội dung:**
- Migration bật extension `unaccent` + `pg_trgm` · cột `SearchVector tsvector` +
  **PostgreSQL TRIGGER** cập nhật khi Title/Description đổi + **GIN index**.
- `SearchRecipesQuery` dùng `EF.Functions.ToTsQuery` với prefix matching (`"pho:* & bo:*"`),
  `ORDER BY ts_rank`, chỉ trả Published.
- Cache TTL **1 phút**, tag `recipes` (D8).
- **Đăng ký route `/recipes/search` TRƯỚC `/recipes/{slug}`** (D10) — nếu không, request
  tới `/recipes/search` sẽ khớp vào route slug.
- Frontend `/search` (SSR).

> Phần khó là cấu hình text search cho tiếng Việt. PostgreSQL không có config `vietnamese`
> sẵn — phải tạo bằng `unaccent` + `simple`. Đây là rủi ro kỹ thuật lớn nhất của slice,
> xử lý ở migration đầu tiên.

**Xong khi:** tìm "pho" ra "Phở bò" · tìm "bo" ra "Bò kho" · xếp theo độ liên quan ·
`q` < 2 ký tự → 400 · ký tự đặc biệt (`'`, `&`, `!`) không làm sập query · không trả Draft ·
`GET /recipes/search` không rơi vào route `{slug}`.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D8, D10 (phần thứ tự đăng ký route).
> Đọc docs/SRS.md FR-SRCH-001 và mục 7.2 (cột SearchVector).
>
> Bước 1 — chỉ làm phần database: viết migration tạo extension unaccent + pg_trgm,
> text search configuration cho tiếng Việt, cột SearchVector, trigger, GIN index.
> Sau đó viết script SQL thủ công kiểm chứng: seed 5 recipe tiếng Việt có dấu,
> chạy truy vấn tìm "pho" và xác nhận khớp "Phở". Chưa động vào C#.
> ```

---

## S11 — SEO + Sitemap + Tracing

**FR phủ:** FR-JOB-003, FR-OBS-003 (2 FR) + NFR-SEO-001→004 · **Phụ thuộc:** S7
**Quyết định:** **D13** (không làm 301 redirect)

**Nội dung:**
- JSON-LD Schema.org Recipe trên `/recipes/[slug]`.
- Meta tags + Open Graph (og:image 1200×630) + canonical + **`noindex` cho draft/archived**.
- `robots.txt`.
- Hangfire recurring `SitemapGenerationJob` cron `0 2 * * *` → sitemap.xml → MinIO + ping GSC.
- OpenTelemetry: HTTP traces, EF Core traces, custom metrics, `TraceId` vào structured log.
- **Không làm 301 redirect và không tạo bảng lưu slug cũ** (D13) — slug bất biến từ S5.

**Xong khi:** Google Rich Results Test pass 100% · sitemap.xml chứa đúng số Published
recipe · trang draft có `noindex` · một request bất kỳ trace được xuyên suốt trong Seq
qua `CorrelationId` + `TraceId`.

> **Prompt:**
> ```
> Đọc docs/decisions.md mục D13.
> Đọc docs/SRS.md mục 4.7 (NFR-SEO-001..004), FR-JOB-003, FR-OBS-003.
>
> Theo D13: BỎ yêu cầu 301 redirect trong NFR-SEO-004. Slug bất biến từ lúc tạo nên
> không cần bảng lưu slug cũ.
>
> Bắt đầu từ JSON-LD: tạo component RecipeJsonLd cho Next.js theo đúng thuộc tính
> Schema.org mà NFR-SEO-001 liệt kê. Tôi sẽ test bằng Rich Results Test trước khi
> làm sitemap.
> ```

---

## S12 — Hardening

**FR phủ:** — (toàn bộ NFR còn lại) · **Phụ thuộc:** tất cả · **Quyết định:** **D15** (rate limit)

**Nội dung:**
- **Rate limiting theo D15:** `/auth/*` 10/phút/IP · API 100/phút/IP · upload 5/phút/IP ·
  header `X-RateLimit-*` + `Retry-After` · 429 `RATE_LIMIT_EXCEEDED`.
- **CORS allowlist** (không `*`), HTTPS + HSTS ở Nginx.
- **5 E2E Playwright**: register, login, create recipe, publish, search.
- **k6**: smoke → load → stress, xác nhận p95 ≤ 500ms và ≥ 100 concurrent users.
- **Lighthouse CI**: LCP ≤ 2.5s, CLS ≤ 0.1, INP ≤ 200ms, bundle ≤ 200KB gzip.
- **axe + NVDA**: WCAG 2.1 AA.
- **Coverage ≥ 80%** tầng Application.
- **Chaos test**: tắt Redis → API vẫn trả đúng, chỉ chậm hơn (D8 + NFR-REL-002).
- **Pre-commit hook** gitleaks · `docker-compose.prod.yml` · README setup < 5 phút · CHANGELOG.

**Xong khi:** tất cả ngưỡng NFR trong `traceability.md` chuyển ✅ hoặc có lý do được ghi
nhận vì sao không đạt.

> **Prompt:**
> ```
> Đọc phần NFR trong docs/traceability.md và docs/decisions.md mục D15.
> Với mỗi NFR còn ⬜, cho biết cần làm gì để đo được nó và công cụ nào đo.
> Xếp theo thứ tự nên làm trước. Chưa code.
> ```

---

## Kiểm tra cuối mỗi slice

Chạy nguyên văn prompt này sau khi hoàn thành một slice:

```
Slice [N] xong. Hãy:
1. Đối chiếu docs/SRS.md + docs/decisions.md với code vừa viết. Có chỗ nào code theo
   SRS trong khi decisions.md đã chốt khác không?
2. Cập nhật docs/traceability.md: trạng thái, đường dẫn file thật, tên test thật.
3. Chạy dotnet test + npm run lint + architecture test. Báo cáo kết quả.
4. Liệt kê TODO còn treo trong code kèm mã FR.
5. Có mâu thuẫn SRS MỚI nào phát hiện trong lúc code không? Thêm mục D23, D24…
   vào docs/decisions.md.
```

## Ba lỗi hay gặp khi chạy lộ trình này

1. **Code theo SRS ở chỗ SRS sai.** SRS vẫn còn nguyên 22 chỗ mâu thuẫn — nó chưa được
   sửa. Mỗi slice đọc mục `decisions.md` tương ứng **trước**, không phải sau.
   Nguy hiểm nhất: D1 (soft delete) ở S7 và D4 (mã lỗi) ở khắp nơi.
2. **Làm S4 không kỹ phần authorization filter.** Bug "Guest thấy Draft của người khác"
   là lỗi bảo mật, và nó lộ ra rất muộn vì seed data thường toàn Published —
   nhớ seed cả Draft và Archived.
3. **Để S12 tới cuối rồi mới đo NFR.** Rate limiting và CORS thì thêm sau được;
   nhưng `p95 ≤ 500ms` và `bundle ≤ 200KB` mà đo lần đầu ở tuần cuối thì thường phải
   sửa kiến trúc. Đo thử ngay từ S4.
