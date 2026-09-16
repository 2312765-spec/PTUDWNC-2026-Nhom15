# Phân công Nhóm — Culinary Blog v1.0.0

**4 người · 8 tuần · chia đều, không tech lead.**
Mỗi người sở hữu trọn một mảng chức năng **từ database lên UI** — không ai chỉ làm backend
hoặc chỉ làm frontend.

> Tài liệu này chia việc. Cách làm việc hằng ngày xem `roadmap.md`; quyết định thiết kế
> xem `decisions.md`; quyền hạn xem `permissions.md`.

---

## 1. Bốn mảng chức năng

| | Người | Mảng | FR | Trọng số thực tế |
|---|---|---|---|---|
| **A** | | **Xác thực & Tài khoản** | 8 | Trung bình — nhiều bẫy bảo mật |
| **B** | | **Khám phá & Tìm kiếm** | 11 | Trung bình — 3 FR chỉ là query param |
| **C** | | **Sáng tạo Công thức** | 7 | **Nặng nhất** — domain logic + wizard UI |
| **D** | | **Media & Vận hành** | 8 | Nặng — chạm hạ tầng, hai đầu việc rời nhau |

> **Đừng đếm số FR để so công bằng.** C chỉ có 7 FR nhưng gánh toàn bộ business rule và
> giao diện phức tạp nhất dự án (wizard nhiều bước, form động kéo-thả). B có 11 FR nhưng
> FR-SRCH-002/003/004 chỉ là query param gắn vào một query có sẵn. Bốn mảng cân nhau
> **về công sức**, không cân về con số.

---

## 2. Chi tiết từng người

### 👤 A — Xác thực & Tài khoản

**FR:** FR-AUTH-001 → 007, FR-JOB-001 · **Slice:** S2, S9, + phần bảo mật của S12
**Quyết định phải đọc:** D5, D20, D4, D11, D17, D9, D12, D15

| Backend | Frontend |
|---|---|
| `ApplicationUser` + `RefreshToken` entity | `/auth/login`, `/auth/register` |
| ASP.NET Core Identity, seed role + Admin | `/profile` — xem/sửa hồ sơ |
| `JwtService` — AT 15p HS256, RT 128-bit SHA-256 hash | Auth.js v5 session provider |
| Token rotation + **reuse detection** (revoke cả family) | Axios interceptor tự refresh khi 401 |
| Google OAuth — verify ID Token, **liên kết** email trùng | Nút "Đăng nhập với Google" |
| Lockout 5 lần/15 phút → 423 | Middleware bảo vệ route `/dashboard`, `/profile` |
| `IEmailService` + `WelcomeEmailJob` (Hangfire) | Form validation với React Hook Form + Zod |
| **Rate limiting, CORS, HSTS, secrets** (S12) | |

**Sở hữu file:** `Application/Auth/**` · `Infrastructure/Identity/**`, `Infrastructure/Jwt/**`,
`Infrastructure/Email/**` · `API/Endpoints/AuthEndpoints.cs` ·
`frontend/app/auth/**`, `frontend/app/profile/**`, `frontend/lib/auth.ts`

**Bốn bẫy dễ sập:**
1. Message đăng nhập sai phải **generic** — lộ "email này chưa đăng ký" là lỗ hổng User Enumeration.
2. Refresh token **không lưu raw**, chỉ lưu SHA-256 hash (D20). Không có cột `IsRevoked`.
3. Email Google trùng tài khoản đã đăng ký thủ công → **`AddLoginAsync` liên kết**, tuyệt đối
   không tạo tài khoản thứ hai.
4. SRS Chương 3 dùng `fullName`/`userName` và trả 422 — **cả hai đều sai** (D5, D4).

---

### 👤 B — Khám phá & Tìm kiếm

**FR:** FR-CAT-001 → 005, FR-RCP-001, FR-RCP-002, FR-SRCH-001 → 004 · **Slice:** S3, S4, S10
**Quyết định phải đọc:** D8, D2, D10, D14, D18

| Backend | Frontend |
|---|---|
| Entity `Category` + `SlugHelper` (tiếng Việt, auto-suffix) | `/` — trang chủ ISR 3600 |
| Category CRUD, soft delete, đếm recipe > 0 → 409 | `/recipes` — SSR, filter + sort + paging |
| **`CachingBehavior` + `CacheInvalidationBehavior`** (Redis, tag-based) | `/recipes/[slug]` — ISR 300 |
| `GetRecipesQuery` — **authorization filter** + 4 filter + sort + paging | `/categories`, `/categories/[slug]` |
| `GetRecipeBySlugQuery` — eager loading, không N+1 | `/search` — SSR |
| Full-text search tiếng Việt: `tsvector` + GIN + trigger + `unaccent` | `/dashboard/categories` — chỉ Admin |
| `PagedResult<T>`, `pageSize` clamp 50 | Component filter bar, pagination, recipe card |

**Sở hữu file:** `Application/Categories/**`, `Application/Recipes/Queries/**` ·
`Infrastructure/Caching/**`, `Infrastructure/Repositories/CategoryRepository.cs` ·
`API/Endpoints/CategoriesEndpoints.cs` ·
`frontend/app/(public)/**`, `frontend/app/search/**`, `frontend/app/dashboard/categories/**`

**Bốn bẫy dễ sập:**
1. **Authorization filter của `GetRecipesQuery` là chỗ nguy hiểm nhất cả dự án.**
   Guest → chỉ Published · Author → thêm Draft/Archived **của chính mình** · Admin → tất cả.
   Sai chỗ này là lỗi bảo mật, và nó không lộ ra nếu seed data toàn Published.
2. **Bạn xây `CachingBehavior` cho cả nhóm dùng.** C và D sẽ gắn `ICacheInvalidator` vào
   command của họ — thiết kế interface ở tuần 2 rồi báo cả nhóm, đừng đổi sau.
3. Sửa tên category phải xóa **cả tag `recipes`** — DTO recipe có nhúng tên category (D8).
4. PostgreSQL **không có** text search config `vietnamese` sẵn. Phải tự tạo bằng
   `unaccent` + `simple`. Đây là rủi ro kỹ thuật lớn nhất của bạn — thử nghiệm bằng SQL
   thuần từ tuần 4, đừng để tuần 7.

---

### 👤 C — Sáng tạo Công thức

**FR:** FR-RCP-003, 004, 005, 006, 007, 009, 010 · **Slice:** S5, S6, S7
**Quyết định phải đọc:** D1, D3, D4, D6, D7, D10, D13, D19

| Backend | Frontend |
|---|---|
| Domain: `Recipe.Create/Update/Publish/Unpublish/Archive` | `/dashboard` — tổng quan Author |
| **`RecipeAuthorizationHandler`** — resource-based auth | `/dashboard/recipes` — danh sách của mình |
| Optimistic concurrency `RowVersion` + `If-Match` → 409 | `/dashboard/recipes/new` — **wizard nhiều bước** |
| Publish: chặn nếu thiếu step **hoặc** ingredient (D3) | `/dashboard/recipes/[id]/edit` |
| Soft delete (D1) — không cascade, không đụng MinIO | Form động steps: thêm/xóa/**kéo-thả sắp xếp** |
| CRUD `RecipeStep` — **auto-renumber `StepNumber`** | Form động ingredients |
| CRUD `RecipeIngredient` — `quantity`/`unit` nullable | Confirm dialog publish/archive/delete |
| | Optimistic update + rollback khi API fail |

**Sở hữu file:** `Domain/Entities/Recipe*.cs` · `Application/Recipes/Commands/**` ·
`Infrastructure/Repositories/RecipeRepository.cs` · `API/Endpoints/RecipesEndpoints.cs` ·
`frontend/app/dashboard/recipes/**`

**Năm bẫy dễ sập:**
1. **Business rule nằm trong Domain method, không nằm trong handler.** `recipe.Publish()`
   ném `DomainException`. Viết unit test cho domain trước — không cần DB, chạy trong 1 giây.
2. **Logic renumber `StepNumber`** là chỗ dễ sai nhất của bạn. Test đủ 4 trường hợp:
   xóa đầu / xóa giữa / xóa cuối / chèn vào giữa.
3. SRS FR-RCP-007 nói "hard delete + cascade + Hangfire xóa MinIO" — **sai cả ba** (D1).
4. SRS validate `cookTime > 0` — sai (D19). Món salad, gỏi có `cookTime = 0`.
5. Slug **bất biến sau khi tạo**, kể cả khi Author đổi title (D13).

> Mảng của bạn nặng nhất. Nếu tuần 5 thấy chậm tiến độ, **báo sớm** — FR-RCP-006 (Archive,
> ưu tiên S) là thứ có thể cắt mà không vỡ demo.

---

### 👤 D — Media & Vận hành

**FR:** FR-RCP-008, FR-FILE-001, 002, FR-JOB-002, 003, FR-OBS-001, 002, 003 · **Slice:** S8, S11
**Quyết định phải đọc:** D22, D16, D1, D13

| Backend | Frontend |
|---|---|
| `IFileStorageService` + `MinioFileStorageService` (AWSSDK.S3) | Component upload + **progress bar realtime** |
| Validation 3 lớp: size → MIME → **magic bytes** | Gallery ảnh, đặt ảnh chính, xóa ảnh |
| 3 endpoint ảnh theo D22 (POST / PATCH / DELETE) | **JSON-LD Schema.org Recipe** |
| Logic `IsPrimary` (ảnh đầu tự động, xóa → ảnh kế lên) | Meta tags, Open Graph, canonical, `noindex` |
| `ImageResizeJob` — medium 800×600 + thumb 300×300 | `app/sitemap.ts`, `robots.txt` |
| `SitemapGenerationJob` — cron `0 2 * * *` + ping GSC | `next/image` optimization |
| Health checks 3 endpoint, Serilog + `CorrelationIdMiddleware` | Lighthouse CI |
| OpenTelemetry — traces, metrics, `TraceId` vào log | |

**Sở hữu file:** `Application/Recipes/Commands/Images/**` · `Infrastructure/Storage/**`,
`Infrastructure/Jobs/**`, `Infrastructure/Observability/**` ·
`API/Endpoints/ImagesEndpoints.cs`, `API/Endpoints/HealthEndpoints.cs` ·
`frontend/components/upload/**`, `frontend/components/seo/**`, `frontend/app/sitemap.ts`

**Bốn bẫy dễ sập:**
1. **Thứ tự validation upload quan trọng:** check size **trước khi đọc stream** (NFR-SEC-004).
   Đọc cả file 5MB vào memory rồi mới kiểm tra là mở đường cho DoS.
2. **Magic bytes, không tin Content-Type header.** File `.exe` đổi tên thành `.jpg` phải bị chặn.
   JPEG `FF D8 FF`, PNG `89 50 4E 47`.
3. Tên file sinh bằng **GUID**, không dùng tên người dùng gửi lên (chống path traversal).
4. SRS FR-RCP-008 có endpoint `/images/{imageId}/primary` — **bỏ** (D22). Dùng một
   `PATCH /images/{imageId}` chung.

---

## 3. Lịch 8 tuần

### Tuần 1 — Sprint 0: Nền chung (cả 4 người, song song, không ai chờ ai)

| Người | Việc |
|---|---|
| **A** | Solution 4 project theo CONS-001 · MediatR + 5 pipeline behavior (khung) · **`GlobalExceptionMiddleware` map exception → HTTP theo D4** · interface `ICurrentUser` |
| **B** | `CulinaryBlogDbContext` · `BaseEntity` · `AuditInterceptor` · **Global Query Filter `IsDeleted`** · **TOÀN BỘ entity theo Chương 7** + migration đầu tiên · seed Bogus 50 recipe / 5 author (**có cả Draft và Archived**) |
| **C** | Next.js App Router + Tailwind · layout + design token · API client (axios) · TanStack Query setup · component chung: Button, Input, Card, Skeleton, Toast, Dialog |
| **D** | `docker-compose.yml` (postgres + redis + minio + seq + mailhog) · Nginx config · health checks · Serilog → Seq · **ArchUnit test Dependency Rule** · GitHub Actions CI |

**Mốc cuối tuần 1:** `docker compose up` → `/health` trả 200 cả 3 component ·
`localhost:3000` render layout · `dotnet test` pass · 0 compiler warning.

> B làm **toàn bộ** entity ở tuần 1, kể cả bảng của slice sau. Một migration lớn sạch hơn
> năm migration vá, và nó gỡ block cho C và D ngay từ tuần 2.

### Tuần 2–3 — Sprint 1: "Đăng nhập được, xem danh mục được"

| Người | Việc |
|---|---|
| **A** | S2 — Identity, JWT, refresh rotation + reuse detection, 6 endpoint + `/auth/login`, `/auth/register`, `/profile` |
| **B** | S3 — Category CRUD + `SlugHelper` + **`CachingBehavior`/`CacheInvalidationBehavior`** + `/categories`, `/categories/[slug]`, `/dashboard/categories` |
| **C** | **Domain layer trước:** `Recipe.Create/Update/Publish/Archive` + unit test đầy đủ (không cần DB, không cần auth) → rồi `CreateRecipeCommand`, `UpdateRecipeCommand`, `RecipeAuthorizationHandler` |
| **D** | S8 backend — `IFileStorageService`, `MinioFileStorageService`, validation 3 lớp, 3 endpoint ảnh |

**Mốc cuối tuần 3:** đăng nhập lấy được token · CRUD danh mục qua UI · upload ảnh qua
Postman thành công · unit test domain Recipe xanh.

> **C không bị block chờ A.** Domain layer không cần authentication. Integration test của C
> dùng `TestAuthenticationHandler` giả lập user cho tới khi A xong cuối tuần 3.

### Tuần 4–5 — Sprint 2: "Tạo được công thức hoàn chỉnh"

| Người | Việc |
|---|---|
| **A** | S9 — Google OAuth (`{ idToken }`) + `WelcomeEmailJob` + MailKit → Mailhog |
| **B** | S4 — `GetRecipesQuery` (authorization filter + 4 filter + sort + paging) + `GetRecipeBySlugQuery` + `/`, `/recipes`, `/recipes/[slug]` |
| **C** | S5 frontend (**wizard tạo/sửa recipe**) + S6 backend & frontend (steps + ingredients, form động) |
| **D** | S8 frontend (upload + progress bar + gallery) + `ImageResizeJob` |

**Mốc cuối tuần 5 — đây là mốc quan trọng nhất cả dự án:** Author tạo được một công thức
hoàn chỉnh có nguyên liệu, các bước và ảnh; Guest xem được ở trang công khai; Draft chỉ
owner thấy.

### Tuần 6–7 — Sprint 3: "Xuất bản, tìm kiếm, sẵn sàng production"

| Người | Việc |
|---|---|
| **A** | Rate limiting (D15) · CORS allowlist · HTTPS + HSTS Nginx · secrets + gitleaks hook · E2E `register`, `login` |
| **B** | S10 — full-text search tiếng Việt + `/search` · E2E `search` |
| **C** | S7 — publish/unpublish/archive/delete + UI nút + confirm dialog · E2E `create recipe`, `publish` |
| **D** | S11 — JSON-LD + meta/OG + `noindex` · `SitemapGenerationJob` · OpenTelemetry tracing |

**Mốc cuối tuần 7:** **feature-complete** — 34/34 FR ở trạng thái ✅ trong `traceability.md`.

### Tuần 8 — Sprint 4: Hoàn thiện & demo

| Người | Việc |
|---|---|
| **A** | Security review chéo toàn hệ thống · coverage tầng Application ≥ 80% |
| **B** | k6: smoke → load → stress · xác nhận p95 ≤ 500ms, ≥ 100 concurrent users · EXPLAIN ANALYZE các query chậm |
| **C** | axe + NVDA — WCAG 2.1 AA · sửa a11y · README setup < 5 phút |
| **D** | Lighthouse CI (LCP/CLS/INP, bundle ≤ 200KB) · `docker-compose.prod.yml` · CHANGELOG |
| **Cả 4** | Chaos test (tắt Redis → API vẫn chạy) · chuẩn bị demo + slide · tổng duyệt |

---

## 4. Hợp đồng phải chốt trong tuần 1

Đây là những thứ cả nhóm dùng chung. **Chốt xong thì đóng băng** — đổi giữa chừng là cả
bốn người phải sửa.

| Hợp đồng | Ai định nghĩa | Ai dùng |
|---|---|---|
| Interface `ICurrentUser` | A | B, C, D |
| Toàn bộ entity + migration | B | A, C, D |
| `ICacheable` / `ICacheInvalidator` | B | C, D |
| `IFileStorageService` | D | C |
| Map exception → HTTP (D4) | A | tất cả |
| Bảng Application Error Code | **`decisions.md` đã chốt sẵn** | tất cả |
| Shape DTO (`RecipeSummaryDto`, `RecipeDetailDto`, `PagedResult<T>`) | B | C (frontend dùng chung type) |
| Design token + component chung | C | A, B, D |

**Cách chốt:** cuối tuần 1 họp 30 phút, mỗi người trình bày interface mình sở hữu, cả nhóm
duyệt, rồi commit. Sau đó muốn đổi phải báo trước trong nhóm chat.

---

## 5. Tránh giẫm chân nhau

### Nhánh Git

```
main
 └── feature/FR-AUTH-002-login        ← A
 └── feature/FR-CAT-003-create        ← B
 └── feature/FR-RCP-005-publish       ← C
 └── feature/FR-RCP-008-images        ← D
```

Một FR một nhánh. Merge vào `main` sau khi có 1 approve. **Không ai push thẳng `main`.**

### File dùng chung — báo trước khi sửa

`Program.cs` · `CulinaryBlogDbContext.cs` · `DependencyInjection.cs` (mỗi tầng) ·
`appsettings.json` · `docker-compose.yml` · `CLAUDE.md` · `frontend/app/layout.tsx`

Sửa những file này thì nhắn nhóm trước, merge ngay trong ngày, đừng để nhánh sống lâu.

### Migration — quy tắc nghiêm ngặt nhất

> **Chỉ một người tạo migration tại một thời điểm.** Nhắn nhóm "mình tạo migration nhé",
> tạo xong, merge vào `main` ngay, rồi báo "xong rồi". Hai migration song song là địa ngục
> để gỡ, và nó luôn xảy ra vào đúng tuần cuối.

Sau khi ai đó merge migration, cả nhóm `git pull` rồi `dotnet ef database update`.

### Review chéo (vòng tròn)

```
A → review B → review C → review D → review A
```

Mỗi người review đúng một người, và được một người review. Vòng tròn đảm bảo **ai cũng
phải đọc code của một mảng khác mình** — đến tuần 8 cả nhóm hiểu được toàn hệ thống,
không ai là hộp đen.

**Checklist review 5 phút:**

- [ ] Có mã FR trong commit message và trong `DisplayName` của test?
- [ ] Có code theo `decisions.md` không, hay code theo SRS ở chỗ SRS sai?
- [ ] Có test cho happy path **và** ít nhất một error case?
- [ ] Endpoint có bảo vệ thì có đủ 4 test phân quyền (`permissions.md` mục 6)?
- [ ] Có `.Include()`/projection không — hay lại N+1?
- [ ] `traceability.md` đã cập nhật chưa?

---

## 6. Nhịp làm việc

| Khi nào | Việc | Bao lâu |
|---|---|---|
| Mỗi ngày | Nhắn nhóm: hôm qua làm gì, hôm nay làm gì, đang kẹt gì | 5 phút, async |
| Cuối mỗi tuần | Cập nhật `traceability.md` — ⬜ → ✅, điền tên file và test thật | 15 phút |
| Cuối mỗi sprint | Demo chéo: mỗi người demo tính năng của mình cho 3 người kia | 30 phút |
| Khi gặp mâu thuẫn SRS mới | Thêm mục `D23`, `D24`… vào `decisions.md`, báo nhóm | ngay lúc đó |

**`traceability.md` là bảng tiến độ chung.** Nhìn vào đó biết ngay ai đang chậm, không cần
hỏi nhau.

---

## 7. Rủi ro và cách xử lý

| Rủi ro | Dấu hiệu sớm | Xử lý |
|---|---|---|
| **C chậm tiến độ** (mảng nặng nhất) | Cuối tuần 5 chưa tạo được recipe hoàn chỉnh | Cắt FR-RCP-006 (Archive, ưu tiên S). A hoặc D sang hỗ trợ phần frontend wizard |
| **Full-text search tiếng Việt không chạy** | B chưa thử SQL thuần trước tuần 5 | Bắt B thử nghiệm bằng SQL thuần **từ tuần 4**. Phương án dự phòng: `ILIKE` + `pg_trgm`, chấp nhận kém chính xác hơn |
| **Migration conflict** | Hai nhánh cùng có file migration mới | Quy tắc mục 5. Nếu đã lỡ: xóa cả hai, tạo lại một cái từ `main` |
| **Bug phân quyền lộ muộn** | Seed data toàn Published | B seed **cả Draft và Archived** ngay tuần 1. Test "Guest không thấy Draft" viết ngay ở S4 |
| **NFR đo lần đầu ở tuần 8** | Không ai chạy Lighthouse/k6 trước tuần 8 | Đo thử từ tuần 5. `p95 ≤ 500ms` và `bundle ≤ 200KB` mà hỏng thì phải sửa kiến trúc, không sửa kịp trong 1 tuần |
| **Một người biến mất** | Không commit 5 ngày | Vòng review chéo đảm bảo có ít nhất 1 người khác đọc qua code đó. Chia lại FR còn thiếu cho 3 người |

---

## 8. Prompt khởi động cho từng người

Dán vào Claude Code khi bắt đầu mảng của mình:

```
Tôi phụ trách mảng [Xác thực & Tài khoản / Khám phá & Tìm kiếm /
Sáng tạo Công thức / Media & Vận hành] trong nhóm 4 người.

Đọc docs/team-assignment.md mục 2 (phần của tôi), rồi đọc docs/decisions.md
các mục được liệt kê trong đó, rồi đọc phần SRS tương ứng.

Tóm tắt lại cho tôi: (1) tôi phải làm những FR nào, (2) quyết định D-x nào
khiến tôi phải làm KHÁC với SRS, (3) tôi phụ thuộc vào ai và ai phụ thuộc
vào tôi. Chưa viết code.
```

Sau đó vào vòng lặp bình thường của `roadmap.md`: Kế hoạch → Test trước → Code → Commit.
