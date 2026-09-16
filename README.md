# Culinary Blog

Nền tảng web chia sẻ công thức nấu ăn. Đồ án môn **Phát triển Ứng dụng Web Nâng cao**.

**.NET 10 Minimal APIs** (Clean Architecture + CQRS) · **Next.js 16 App Router** ·
PostgreSQL 16 · Redis 7 · MinIO · Docker Compose

---

## Bắt đầu trong 5 phút

### Cần cài sẵn

| | Phiên bản | Kiểm tra |
|---|---|---|
| .NET SDK | 10.0.x | `dotnet --version` |
| Node.js | 22 LTS | `node -v` |
| Docker Desktop | 27+ | `docker --version` |

### Các bước

```bash
git clone <URL-repo> culinary-blog && cd culinary-blog

# 1. Bật hạ tầng (postgres, redis, minio, seq, mailhog)
docker compose up -d

# 2. Backend  →  http://localhost:5000/scalar
dotnet restore CulinaryBlog.sln
dotnet run --project backend/CulinaryBlog.API

# 3. Frontend (terminal khác)  →  http://localhost:3000
cd frontend && npm ci && npm run dev
```

### Kiểm tra đã chạy đúng

```bash
curl http://localhost:5000/health        # {"status":"Healthy", ...}
./scripts/verify.sh                      # build + test + lint toàn bộ
```

> **Trạng thái kiểm chứng của khung này**
> Frontend đã được build/lint/typecheck thật: `npm run build`, `npm run lint`,
> `npm run typecheck` đều xanh, 0 warning.
> Backend được viết theo chuẩn .NET 10 nhưng **chưa chạy `dotnet build` lần nào** —
> môi trường dựng khung không tải được .NET SDK. Người làm Sprint 0 chạy
> `./scripts/verify.sh` đầu tiên; nếu `dotnet restore` báo không tìm thấy version
> package thì sửa **duy nhất** `Directory.Packages.props` rồi commit —
> mọi version tập trung ở đúng một file để việc này chỉ mất vài phút.

| Dịch vụ | URL |
|---|---|
| API | http://localhost:5000 |
| **API docs (Scalar)** | http://localhost:5000/scalar |
| Frontend | http://localhost:3000 |
| Xem log (Seq) | http://localhost:5341 |
| Xem email (Mailhog) | http://localhost:8025 |
| MinIO Console | http://localhost:9001 — `minioadmin` / `minioadmin` |

> Mọi endpoint hiện trả **501 Not Implemented** kèm mã FR và người phụ trách.
> Đó là chủ ý: Scalar UI hiển thị đầy đủ API contract ngay từ ngày đầu để 4 người
> làm song song, và không ai nhầm endpoint rỗng là đã xong.

---

## Đọc gì trước khi viết dòng code đầu tiên

| Thứ tự | File | Nội dung |
|---|---|---|
| 1 | `docs/team-assignment.md` | Bạn phụ trách mảng nào, lịch 8 tuần, quy tắc tránh giẫm chân |
| 2 | `docs/decisions.md` | **22 quyết định chốt các chỗ SRS tự mâu thuẫn** |
| 3 | `docs/roadmap.md` | 12 slice, prompt mẫu cho từng slice |
| 4 | `docs/traceability.md` | 34 FR → endpoint → file → test → trạng thái |
| 5 | `docs/permissions.md` | Ma trận quyền Guest/Author/Admin |
| — | `CLAUDE.md` | Claude Code tự đọc, bạn không cần mở |

> ⚠️ **SRS bản PDF còn nguyên 22 chỗ sai** — nó chưa được sửa.
> Thứ tự ưu tiên khi hai nguồn nói khác nhau:
> `docs/decisions.md` → `docs/SRS.md` → **dừng và hỏi** (không suy đoán).

**Ba quyết định trái với SRS nhiều nhất, nhớ kỹ:**

- Xóa Recipe/Category là **soft delete** — SRS FR-RCP-007 nói hard delete + cascade + xóa MinIO, **sai cả ba** (D1)
- Validation lỗi trả **400**, concurrency trả **409** — **422 không dùng ở bất kỳ đâu** (D4)
- Cache **chỉ Redis** qua `CachingBehavior` — cấm `IMemoryCache` và Output Cache (D8)

---

## Cấu trúc thư mục

```
culinary-blog/
├── CLAUDE.md                     Ngữ cảnh cho Claude Code (tự nạp)
├── CulinaryBlog.sln
├── Directory.Build.props         TargetFramework, Nullable, TreatWarningsAsErrors
├── Directory.Packages.props      ⭐ MỌI version package nằm ở đây (Central Package Management)
├── docker-compose.yml
├── scripts/verify.sh             Chạy trước khi push
│
├── docs/                         Tài liệu — đọc trước khi code
│
├── backend/
│   ├── CulinaryBlog.Domain/          Entity, ValueObject, Enum, DomainException
│   │                                 ⛔ KHÔNG có PackageReference nào (CONS-001)
│   ├── CulinaryBlog.Application/     Command, Query, Handler, DTO, Validator, Behavior
│   │   ├── Common/Behaviors/         4 pipeline behavior (thứ tự cố định)
│   │   ├── Common/Interfaces/        ⭐ Hợp đồng chung giữa 4 người
│   │   ├── Auth/         → A
│   │   ├── Categories/   → B
│   │   └── Recipes/{Queries → B, Commands → C, Commands/Images → D}
│   ├── CulinaryBlog.Infrastructure/  DbContext, Repository, Redis, MinIO, Jwt, Hangfire
│   ├── CulinaryBlog.API/             Minimal API endpoints, Middleware, Program.cs
│   └── tests/
│       ├── CulinaryBlog.UnitTests/
│       ├── CulinaryBlog.IntegrationTests/
│       └── CulinaryBlog.ArchitectureTests/   ⭐ Kiểm tra CONS-001, chạy trong CI
│
└── frontend/
    ├── app/
    │   ├── (public)/  → B       auth/ → A       dashboard/ → C + B
    ├── components/{ui → C, upload → D, seo → D}
    └── lib/{api-client, types, utils}   ⭐ Hợp đồng chung
```

Mỗi thư mục có chủ sở hữu đều có file `OWNER.md` ghi rõ: ai phụ trách, FR nào,
quyết định D-x nào phải đọc, và những bẫy dễ sập. **Xóa `OWNER.md` khi làm xong thư mục đó.**

---

## Quy tắc làm việc nhóm

### Nhánh và commit

```bash
git switch -c feature/FR-CAT-003-create-category
git commit -m "feat(FR-CAT-003): thêm endpoint tạo danh mục"
```

Một FR một nhánh. Merge vào `main` sau **1 approve**. Không push thẳng `main`.

**Review vòng tròn:** A → B → C → D → A. Mỗi người review đúng một người.

### ⚠️ Migration — quy tắc nghiêm ngặt nhất

> **Chỉ một người tạo migration tại một thời điểm.** Nhắn nhóm "mình tạo migration nhé",
> tạo xong, merge vào `main` ngay, rồi báo "xong rồi".
> Hai migration song song rất khó gỡ, và nó luôn xảy ra vào đúng tuần cuối.

```bash
dotnet ef migrations add <Tên> \
  --project backend/CulinaryBlog.Infrastructure \
  --startup-project backend/CulinaryBlog.API

dotnet ef database update \
  --project backend/CulinaryBlog.Infrastructure \
  --startup-project backend/CulinaryBlog.API
```

Sau khi ai đó merge migration: `git pull` rồi `dotnet ef database update`.

### File dùng chung — nhắn nhóm trước khi sửa

`Program.cs` · `CulinaryBlogDbContext.cs` · `DependencyInjection.cs` · `appsettings.json` ·
`docker-compose.yml` · `Directory.Packages.props` · `frontend/app/layout.tsx`

### Checklist review (5 phút)

- [ ] Commit message và `DisplayName` của test có mã FR?
- [ ] Code theo `decisions.md`, không theo SRS ở chỗ SRS sai?
- [ ] Có test happy path **và** ít nhất 1 error case?
- [ ] Endpoint có bảo vệ → đủ 4 test phân quyền (`docs/permissions.md` mục 6)?
- [ ] Query EF Core có `.Include()`/projection — không N+1?
- [ ] `docs/traceability.md` đã cập nhật?

---

## Lệnh hay dùng

```bash
# Backend
dotnet build CulinaryBlog.sln                                  # phải 0 warning
dotnet test                                                     # toàn bộ test
dotnet test backend/tests/CulinaryBlog.ArchitectureTests        # kiểm tra CONS-001
dotnet run --project backend/CulinaryBlog.API

# Frontend
cd frontend
npm run dev · npm run lint · npm run typecheck · npm run build

# Hạ tầng
docker compose up -d          # chỉ dependency
docker compose --profile full up --build    # cả API + frontend trong container
docker compose down -v        # xóa sạch, kể cả dữ liệu

# Trước khi push
./scripts/verify.sh
```

### Secrets — không bao giờ commit (NFR-SEC-007)

```bash
cd backend/CulinaryBlog.API
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Google:ClientId" "<...>"
```

---

## Ràng buộc kiến trúc — vi phạm là build đỏ

| Mã | Ràng buộc |
|---|---|
| CONS-001 | Domain **không** phụ thuộc thư viện ngoài nào. Application **không** reference Infrastructure |
| CONS-002 | CQRS + MediatR. Mỗi use case = 1 Command hoặc 1 Query handler riêng |
| CONS-003 | Backend Minimal APIs (không MVC Controllers). Frontend App Router (không Pages Router) |
| CONS-005 | RESTful + RFC 7807. Version qua URL path `/api/v1/` |
| CONS-006 | PostgreSQL duy nhất. EF Core Code-First. Không raw SQL không parameterize |
| CONS-007 | Upload ≤ 5MB. Kiểm MIME bằng **magic bytes**, không tin extension |
| CONS-008 | Validation qua FluentValidation + `ValidationBehavior`. **Không** validate trong endpoint |
| CONS-010 | Serilog. Mọi log có `CorrelationId`, `RequestPath`, `UserId` |

`CulinaryBlog.ArchitectureTests` kiểm tra CONS-001 tự động trong CI.

---

## Khắc phục sự cố

**`dotnet restore` báo không tìm thấy version package**
→ Sửa **duy nhất** `Directory.Packages.props`. Mọi version tập trung ở đó, `.csproj` không ghi version.

**Port đã bị chiếm (5432 / 6379 / 9000)**
→ `docker compose down` rồi kiểm tra: `lsof -i :5432` (macOS/Linux) · `netstat -ano | findstr :5432` (Windows).

**API không kết nối được database**
→ `docker compose ps` xem postgres đã `healthy` chưa. Chưa thì `docker compose logs postgres`.

**Frontend gọi API bị lỗi CORS**
→ Kiểm tra `Cors:AllowedOrigins` trong `appsettings.Development.json` có `http://localhost:3000`.

**Test tìm kiếm tiếng Việt fail**
→ Extension `unaccent`/`pg_trgm` chỉ được tạo khi volume postgres **còn rỗng**.
Đã lỡ tạo volume trước đó thì `docker compose down -v` rồi `up -d` lại.
