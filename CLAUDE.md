# CLAUDE.md — Culinary Blog (CULINARY-BLOG-V1)

> File này là bộ nhớ thường trực của dự án. Claude đọc nó tự động mỗi phiên.
> Giữ nó ngắn và luôn đúng. Không nhét nội dung SRS vào đây — chỉ trỏ tới SRS.

---

## 1. Nguồn sự thật (Source of Truth)

| Tài liệu | Vai trò | Quyền sửa |
|---|---|---|
| `docs/SRS.md` | SRS v1.0.0 (Approved 04/06/2026) — yêu cầu gốc | **CHỈ ĐỌC.** Chỉ sửa qua quy trình Change Request |
| `docs/traceability.md` | 34 FR → endpoint → file → test → trạng thái | Cập nhật sau mỗi slice |
| `docs/permissions.md` | Ma trận phân quyền Guest/Author/Admin | Cập nhật khi đổi rule |
| `docs/roadmap.md` | Lộ trình vertical slice | Cập nhật khi đổi thứ tự |
| `docs/team-assignment.md` | Phân công 4 người, lịch 8 tuần, quy tắc tránh giẫm chân | Cập nhật khi đổi phân công |
| **`docs/decisions.md`** | **22 quyết định chốt các mâu thuẫn trong SRS** | Thêm D23, D24… khi phát hiện mâu thuẫn mới |
| `docs/adr/` | Architecture Decision Records | Thêm 1 file mỗi quyết định kiến trúc |
| `docs/plans/` | Kế hoạch implement từng FR (bước Plan, xem mục 9) | Chỉ tạo file khi người dùng đồng ý — xem mục 9 |

### Quy tắc vàng

1. **Thứ tự ưu tiên khi hai nguồn nói khác nhau:**
   `docs/decisions.md` → `docs/SRS.md` → **DỪNG và hỏi** (không được suy đoán).
   SRS v1.0.0 có 22 chỗ tự mâu thuẫn; `decisions.md` đã chốt cả 22 và **thắng SRS**
   ở mọi chỗ đó.
2. **Mọi dòng code phải truy được về một mã FR hoặc NFR.** Không có FR → không code.
3. **Gặp mâu thuẫn SRS mới trong lúc code:** không tự xử lý im lặng. Thêm mục `D23`,
   `D24`… vào `docs/decisions.md` với lý do, rồi mới code theo.
4. **Không sửa `docs/SRS.md`.** Muốn đổi yêu cầu → đổi SRS trước qua CR, rồi mới đổi code.
   (Danh sách chỗ cần sửa trong SRS đã liệt kê sẵn ở cuối mỗi mục `decisions.md`.)

### Tra nhanh 8 quyết định hay dùng nhất

| Chủ đề | Chốt | Chi tiết |
|---|---|---|
| Xóa Recipe / Category | **Soft delete** (`IsDeleted = true`), không cascade, không xóa file MinIO | D1, D2 |
| Validation lỗi | **400** `VALIDATION_ERROR` — **không dùng 422 ở bất kỳ đâu** | D4 |
| Concurrency conflict | **409** `RECIPE_CONCURRENCY_CONFLICT` | D4 |
| Cache | **Chỉ Redis** qua `CachingBehavior`. Cấm `IMemoryCache` và Output Cache | D8 |
| Trường hồ sơ user | `displayName`, `avatarUrl`, `bio` — **không có** `fullName`, `userName` | D5 |
| Slug trùng | Auto-suffix `-2`, `-3`… — không bao giờ trả 409 | D10 |
| Điều kiện publish | Cần **≥ 1 step VÀ ≥ 1 ingredient** | D3 |
| Ngoài scope v1 | Admin quản lý user · xác nhận email · policy `VerifiedAuthor` · 301 redirect slug | D11, D12, D13 |

---

## 2. Tech stack (cố định, không thương lượng)

**Backend** — .NET 10, C#, **Minimal APIs** (KHÔNG dùng MVC Controllers)
- Clean Architecture 4 tầng + CQRS qua MediatR
- EF Core 10 Code-First, PostgreSQL 16 (DBMS duy nhất)
- ASP.NET Core Identity + JWT (HS256) + Google OAuth 2.0
- FluentValidation, Serilog, OpenTelemetry, Hangfire
- Scalar UI tại `/scalar` cho API docs

**Frontend** — Next.js 15 **App Router** (KHÔNG dùng Pages Router)
- TypeScript, Tailwind CSS (utility-first, không CSS framework override)
- Auth.js v5, TanStack Query, React Hook Form + Zod

**Hạ tầng** — Redis 7 (cache), MinIO (S3-compatible, object storage),
Nginx (reverse proxy + SSL), Docker Compose

---

## 3. Ràng buộc bất biến (CONS-001 → CONS-010)

Đây là 10 ràng buộc SRS ghi rõ là **không thể thương lượng**. Vi phạm = reject PR.

| Mã | Ràng buộc |
|---|---|
| CONS-001 | Clean Architecture 4 tầng. **Domain KHÔNG phụ thuộc thư viện ngoài nào** (chỉ .NET BCL) |
| CONS-002 | CQRS + MediatR bắt buộc. Mỗi use case = 1 Command hoặc 1 Query handler riêng |
| CONS-003 | Backend: Minimal APIs. Frontend: App Router. Không lệch |
| CONS-004 | JWT stateless. Access token 15 phút, refresh token 7 ngày. Password hash PBKDF2 qua Identity |
| CONS-005 | RESTful + RFC 7807 (`application/problem+json`). Version qua URL path `/api/v1/` |
| CONS-006 | PostgreSQL duy nhất. Migrations EF Core Code-First. **Không raw SQL** (LINQ, hoặc raw SQL có parameterization) |
| CONS-007 | Upload tối đa **5 MB**. Chỉ `image/jpeg`, `image/png`, `image/webp`, `image/avif`. **Kiểm tra MIME bằng magic bytes**, không tin extension hay Content-Type header |
| CONS-008 | Validation qua **FluentValidation + ValidationBehavior** trong MediatR pipeline. **KHÔNG validate trong endpoint handler** |
| CONS-009 | Docker hóa. Dockerfile multi-stage (SDK → aspnet runtime) |
| CONS-010 | Serilog structured logging. Mọi log entry PHẢI có `CorrelationId`, `RequestPath`, `UserId` (nếu đã xác thực) |

### Dependency Rule (kiểm tra bằng ArchUnit.NET test)

```
Domain          ← không tham chiếu ai
Application     → Domain (chỉ vậy). KHÔNG reference Infrastructure
Infrastructure  → Application, Domain
API             → Application, Infrastructure, Domain
```

### MediatR pipeline — thứ tự cố định

```
1. LoggingBehavior          (mọi Command + Query)
2. ValidationBehavior       (mọi request có Validator)
3. CachingBehavior          (Query implements ICacheable)
4. Handler                  (IRequestHandler)
5. CacheInvalidationBehavior (Command implements ICacheInvalidator)
```

---

## 4. Cấu trúc thư mục

```
/
├── CLAUDE.md
├── docs/
│   ├── SRS.md                  ← chỉ đọc
│   ├── traceability.md
│   ├── permissions.md
│   ├── roadmap.md
│   ├── decisions.md            ← chốt 22 mâu thuẫn của SRS, thắng SRS
│   ├── team-assignment.md
│   ├── adr/
│   └── plans/                  ← 1 file .md mỗi FR, chỉ tạo khi được đồng ý (mục 9)
├── backend/
│   ├── CulinaryBlog.Domain/          Entities, ValueObjects, Enums, IRepository<T>
│   ├── CulinaryBlog.Application/     Commands, Queries, Handlers, DTOs, Validators, Behaviors
│   ├── CulinaryBlog.Infrastructure/  DbContext, Migrations, Repositories, JwtService,
│   │                                 MinioFileStorageService, RedisCacheService, Hangfire jobs
│   ├── CulinaryBlog.API/             Endpoint groups, Middleware, Program.cs, DI extensions
│   └── tests/
│       ├── CulinaryBlog.UnitTests/           xUnit — handlers, validators, domain
│       ├── CulinaryBlog.IntegrationTests/    WebApplicationFactory + Testcontainers
│       └── CulinaryBlog.ArchitectureTests/   ArchUnit.NET — CONS-001, NFR-MAINT-004
├── frontend/
│   ├── app/                    App Router routes
│   ├── components/
│   ├── lib/                    api client, auth, utils
│   └── __tests__/              Jest + Testing Library
├── e2e/                        Playwright
├── docker-compose.yml          dev
├── docker-compose.prod.yml
└── nginx/
```

**Đặt tên:** Command/Query `{Verb}{Entity}{Command|Query}` (`CreateRecipeCommand`,
`GetRecipeBySlugQuery`). Handler cùng tên + `Handler`. Validator cùng tên + `Validator`.
Mỗi handler một file. Endpoint group `{Entity}Endpoints.cs`.

---

## 5. Lệnh hay dùng

```bash
# Hạ tầng dev (postgres, redis, minio, seq, mailhog)
docker compose up -d

# Backend
dotnet restore
dotnet build                                          # phải 0 warning (NFR-MAINT-001)
dotnet run   --project backend/CulinaryBlog.API       # http://localhost:5000
dotnet test                                           # toàn bộ test
dotnet test backend/tests/CulinaryBlog.ArchitectureTests   # kiểm tra CONS-001

# Migration
dotnet ef migrations add <Tên> \
  --project backend/CulinaryBlog.Infrastructure \
  --startup-project backend/CulinaryBlog.API
dotnet ef database update \
  --project backend/CulinaryBlog.Infrastructure \
  --startup-project backend/CulinaryBlog.API

# Frontend
cd frontend && npm run dev      # http://localhost:3000
npm run lint && npm run build
npx playwright test             # E2E
```

**Cổng:** API 5000 · Web 3000 · Postgres 5432 · Redis 6379 · MinIO 9000/9001 ·
Seq 5341 · Mailhog 8025

---

## 6. Quy tắc code

### Backend

- **Endpoint handler chỉ làm 3 việc:** nhận request → `mediator.Send(command)` → trả
  `TypedResults`. Không business logic, không validation, không truy cập DbContext.
- **Domain entity không có public setter.** Tạo qua factory method `Recipe.Create(...)`,
  đổi trạng thái qua method `recipe.Publish()`, `recipe.Archive()`.
- **Business rule nằm trong Domain**, không nằm trong handler. Ví dụ: "không publish
  recipe thiếu step" là `recipe.Publish()` ném `DomainException`, không phải if trong handler.
- **Mọi query EF Core phải có `.Include()`/`.ThenInclude()` hoặc projection** — cấm N+1
  (NFR-PERF-004). Query đọc thêm `.AsNoTracking()`.
- **Không trả entity ra ngoài API.** Luôn map sang DTO.
- **Không bao giờ trả `PasswordHash`, `SecurityStamp`, raw refresh token.**
- **Lỗi trả về theo RFC 7807** với `type` = Application Error Code (bảng cuối
  `docs/decisions.md`, dạng `SCREAMING_SNAKE_CASE`), không hardcode chuỗi tiếng Việt.

### Map exception → HTTP (GlobalExceptionMiddleware)

| Exception | HTTP | Error code |
|---|---|---|
| `ValidationException` (FluentValidation) | **400** | `VALIDATION_ERROR` |
| `DomainException` (vi phạm business rule) | **400** | theo từng rule |
| `UnauthorizedException` | 401 | `AUTH_TOKEN_INVALID` |
| `ForbiddenException` | 403 | `RECIPE_FORBIDDEN`… |
| `NotFoundException` | 404 | `*_NOT_FOUND` |
| `ConflictException` | 409 | `*_EXISTS`, `CATEGORY_DELETE_HAS_RECIPES` |
| `DbUpdateConcurrencyException` | **409** | `RECIPE_CONCURRENCY_CONFLICT` |
| Còn lại | 500 | — (log đầy đủ, **không lộ stack trace**) |

**422 không được dùng ở bất kỳ đâu.** Gặp `422` trong SRS Chương 3 thì đọc lại D4.

### Cache — cấu hình cố định (D8)

Chỉ dùng Redis, truy cập qua `CachingBehavior` (Query implements `ICacheable`) và
`CacheInvalidationBehavior` (Command implements `ICacheInvalidator`).

| Key | TTL | Tag |
|---|---|---|
| `categories:all` | 30 phút | `categories` |
| `recipes:list:{hash(query)}` | 15 phút | `recipes` |
| `recipe:{slug}` | 5 phút | `recipes`, `recipe:{slug}` |
| `recipes:search:{hash}` | 1 phút | `recipes` |

Invalidation: Command trên **Category** xóa tag `categories` **và** `recipes`
(vì DTO recipe có nhúng tên category). Command trên **Recipe / Step / Ingredient / Image**
xóa `recipes` và `recipe:{slug}`.

**Redis down không được làm sập request** — log warning rồi đi thẳng xuống handler
(NFR-REL-002).

### Frontend

- **Server Component mặc định.** Chỉ thêm `"use client"` khi thực sự cần state/effect/event.
- **Rendering theo đúng bảng 5.1 của SRS:** `/` ISR 3600 · `/recipes` SSR ·
  `/recipes/[slug]` ISR 300 · `/categories` ISR 3600 · `/categories/[slug]` ISR 600 ·
  `/dashboard/*` CSR · `/search` SSR.
- **Mọi async operation phải có loading state** (skeleton, không blank screen) và
  toast xác nhận sau write (NFR-USE-004).
- **Trang recipe phải có JSON-LD Schema.org Recipe** + Open Graph (NFR-SEO-001/002).
- **Draft/Archived → `noindex`.**
- Accessibility WCAG 2.1 AA: semantic HTML5, aria-label, điều hướng bàn phím đầy đủ.

### Bảo mật — không có ngoại lệ

- **Không commit secrets.** Dev dùng .NET User Secrets, prod dùng env vars.
- **Phân quyền kiểm tra ở Application Layer**, không chỉ ở endpoint (NFR-SEC-006).
  Resource ownership qua `IAuthorizationService.AuthorizeAsync(user, recipe, Operations.Update)`.
- **Không dùng chuỗi role hardcode.** Dùng policy: `"AuthorPolicy"`, `"AdminPolicy"`.
- **Login sai → message generic** `AUTH_INVALID_CREDENTIALS`, không tiết lộ email có
  tồn tại hay không (chống User Enumeration).
- **Tên file upload sinh bằng GUID**, không dùng tên người dùng gửi lên (chống path traversal).
- **Refresh token lưu DB dưới dạng SHA-256 hash**, không lưu raw token.

---

## 7. Test — bắt buộc trước khi merge

Ngưỡng từ NFR-MAINT-002:

| Loại | Yêu cầu | Công cụ |
|---|---|---|
| Unit | ≥ 80% line coverage tầng Application | xUnit |
| Integration | Mỗi endpoint ≥ 1 happy path + 1 error case | xUnit + WebApplicationFactory + Testcontainers |
| E2E | 5 luồng: register, login, create recipe, publish, search | Playwright |
| Architecture | Dependency Rule (CONS-001) | ArchUnit.NET |

**Mỗi test ghi rõ nó phủ FR nào:**

```csharp
[Fact(DisplayName = "FR-RCP-005/D3: không publish được recipe chưa có step")]
public async Task Publish_WithoutSteps_Returns400() { ... }
```

**Định nghĩa "xong" của một FR:** code chạy + test pass + `traceability.md` cập nhật +
0 compiler warning + lint pass.

---

## 8. Quy ước Git

```
<type>(<mã FR>): <mô tả ngắn>

feat(FR-RCP-003): thêm endpoint tạo recipe mới
fix(FR-AUTH-004): xử lý refresh token reuse detection
test(FR-CAT-005): thêm case xóa category còn recipe
chore(CONS-001): thêm architecture test kiểm tra dependency rule
docs: cập nhật traceability sau slice 3
```

- Branch: `feature/FR-RCP-003-create-recipe`
- Commit nhỏ, thường xuyên. Commit sau mỗi bước chạy được.
- PR cần ≥ 1 reviewer (NFR-MAINT-001).

---

## 9. Cách làm việc với Claude trên dự án này

**Vòng lặp chuẩn cho mỗi slice:**

1. **Explore** — "Đọc `docs/SRS.md` phần FR-xxx và `docs/decisions.md`. Tóm tắt
   yêu cầu, nêu rõ quyết định D-x nào áp dụng cho FR này. Chưa viết code."
2. **Plan** — "Lập kế hoạch implement FR-xxx. Liệt kê file sẽ tạo/sửa theo từng tầng.
   Chưa code." → người review kế hoạch. Trình bày kế hoạch xong, **hỏi người dùng có
   đồng ý lưu kế hoạch này thành file `docs/plans/FR-xxx-<mô-tả-ngắn>.md` không** —
   **chỉ tạo file khi được đồng ý**, không tự ý tạo. Đồng ý thì tạo file trước, rồi
   mới sang bước Test first.
3. **Test first** — "Viết integration test cho FR-xxx từ SRS. Chạy, xác nhận fail."
4. **Code** — implement đến khi test pass.
5. **Commit** — cập nhật `traceability.md`, commit.

**Không làm:**

- Không code nhiều FR cùng lúc trong một lần.
- Không đọc cả 71 trang SRS vào context. Chỉ đọc phần FR đang làm.
- Không tự thêm tính năng ngoài scope. Ngoài scope v1.0.0: comment, rating,
  bookmark, realtime/SignalR, mobile native, thanh toán, chat, GraphQL.
- Không refactor kiến trúc mà không có ADR.

**Kiểm tra định kỳ (cuối mỗi sprint):**

> Đối chiếu `docs/SRS.md` + `docs/decisions.md` với codebase. FR nào đã xong, FR nào chưa,
> FR nào implement khác mô tả? Có chỗ nào code theo SRS trong khi `decisions.md` đã chốt
> khác không? Cập nhật `docs/traceability.md`.
