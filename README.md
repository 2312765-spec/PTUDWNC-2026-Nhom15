# Culinary Blog

Blog chia sẻ công thức nấu ăn — Guest xem và tìm kiếm công thức, Author viết / xuất bản công thức có nguyên liệu, các
bước và ảnh, Admin quản lý danh mục. Đồ án môn Phát triển ứng dụng Web nâng cao (PTUDWNC 2026, Nhóm 15).

| Tầng | Công nghệ |
|---|---|
| Backend | .NET 10 Minimal APIs · Clean Architecture 4 tầng · CQRS/MediatR · EF Core 10 · PostgreSQL 16 · Identity + JWT · Hangfire · Serilog · OpenTelemetry |
| Frontend | Next.js 16 App Router · TypeScript · Tailwind CSS 4 · Auth.js v5 · TanStack Query · React Hook Form + Zod |
| Hạ tầng | Redis 7 (cache) · S3 (dev: s3mock) · Seq (log) · Mailhog (email) · Nginx · Docker Compose |

## Yêu cầu cài sẵn

| Công cụ | Phiên bản | Kiểm tra |
|---|---|---|
| Docker Desktop | có Docker Compose v2 | `docker compose version` |
| .NET SDK | 10 | `dotnet --version` |
| Node.js | ≥ 20.9 | `node -v` |
| Git | bất kỳ | `git --version` |
| gitleaks *(tùy chọn)* | bất kỳ | `gitleaks version` — cho hook chặn commit secret |

> Chỉ muốn chạy thử, không viết code? Chỉ cần Docker — xem [Cách B](#cách-b--chỉ-cần-docker).

## Chạy trong 5 phút (cách A — dev, có hot-reload)

Mở Docker Desktop trước. Các lệnh dưới chạy được trên Git Bash, macOS/Linux; PowerShell xem ghi chú ở bước 4.

**1. Lấy mã nguồn**

```bash
git clone https://github.com/2312765-spec/PTUDWNC-2026-Nhom15.git culinary-blog
cd culinary-blog
git config core.hooksPath .githooks   # bật hook quét secret (cần gitleaks)
```

**2. Bật hạ tầng** — Postgres, Redis, S3, Seq, Mailhog

```bash
docker compose up -d
```

**3. Chạy API** (terminal 1) — giữ terminal này mở

```bash
dotnet run --project backend/CulinaryBlog.API
```

Lần đầu API tự **migrate database và seed dữ liệu mẫu** (50 công thức, 5 author, 1 admin). Không cần User Secrets:
`appsettings.Development.json` đã có cấu hình dev (khóa JWT chỉ dùng cho dev). Xong khi thấy
`Now listening on: http://localhost:5000`.

**4. Cấu hình frontend** (terminal 2)

```bash
cd frontend
cp .env.example .env.local
echo "AUTH_SECRET=$(node -e "console.log(require('crypto').randomBytes(32).toString('base64'))")" >> .env.local
```

PowerShell: `Copy-Item .env.example .env.local` rồi mở `.env.local`, điền `AUTH_SECRET=` bằng kết quả của
`node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"`.

`AUTH_GOOGLE_ID` / `AUTH_GOOGLE_SECRET` để trống được — chỉ nút "Đăng nhập bằng Google" không dùng được.

**5. Chạy frontend**

```bash
npm ci
npm run dev
```

Mở <http://localhost:3000>.

### Đăng nhập thử

Mật khẩu chung: `Password123!` (dữ liệu seed, chỉ có ở môi trường Development)

| Vai trò | Email |
|---|---|
| Author | `author1@seed.culinaryblog.local` … `author5@seed.culinaryblog.local` |
| Admin | `admin@seed.culinaryblog.local` |

Hoặc tự đăng ký tài khoản mới ở `/auth/register` (tài khoản mới là Author).

### Địa chỉ

| Dịch vụ | URL |
|---|---|
| Web | <http://localhost:3000> |
| API | <http://localhost:5000/api/v1> |
| Tài liệu API (Scalar) | <http://localhost:5000/scalar> |
| Health check | <http://localhost:5000/health> · `/health/live` · `/health/ready` |
| Log (Seq) | <http://localhost:5341> |
| Email gửi đi (Mailhog) | <http://localhost:8025> |
| Postgres · Redis · S3 | `localhost:5432` (postgres / postgres) · `localhost:6379` · `localhost:9000` |

## Cách B — chỉ cần Docker

Chạy cả API và frontend trong container (không hot-reload). Lần đầu phải build image nên lâu hơn cách A.

```bash
git clone https://github.com/2312765-spec/PTUDWNC-2026-Nhom15.git culinary-blog
cd culinary-blog
docker compose --profile full up -d --build
```

Mở <http://localhost:3000>, đăng nhập như trên. Tắt: `docker compose --profile full down`.

## Chạy test

```bash
dotnet test                                   # backend: unit + integration (Testcontainers, cần Docker) + architecture
cd frontend && npm test                       # frontend: Jest + Testing Library
npm run lint && npm run build                 # lint + build production
npm run test:e2e -- --workers=1               # E2E Playwright — cần API (bước 3) đang chạy
./scripts/verify.sh                           # kiểm tra tổng hợp trước khi push
```

Lần đầu chạy E2E: `npx playwright install chromium`.

## Sự cố thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| API dừng ngay, log có `Npgsql` / `Failed to connect` | Docker Desktop chưa bật hoặc container chưa lên — `docker compose up -d`, đợi `docker compose ps` báo `healthy` |
| Đăng nhập xong trang trắng, console báo `/api/auth/session` **404** | `.next` còn sót bản `npm run build` — tắt `npm run dev`, xóa thư mục `frontend/.next`, chạy lại |
| Frontend báo lỗi `MissingSecret` / không đăng nhập được | Thiếu `AUTH_SECRET` trong `frontend/.env.local` (bước 4) |
| `address already in use` / cổng 5000, 3000, 5432… bị chiếm | Tắt chương trình đang dùng cổng đó, hoặc lần trước chưa tắt `dotnet run` / `npm run dev` |
| E2E fail hàng loạt kiểu "bấm link nhưng không chuyển trang" | `next dev` quá tải khi chạy song song — dùng `--workers=1` |
| Muốn làm lại database từ đầu | `docker compose down -v` (xóa cả dữ liệu) rồi làm lại từ bước 2 |

## Tài liệu

| File | Nội dung |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | Quy tắc dự án: kiến trúc, ràng buộc CONS-001→010, quy ước code / Git / test |
| [`docs/SRS.md`](docs/SRS.md) | Yêu cầu gốc (chỉ đọc) |
| [`docs/decisions.md`](docs/decisions.md) | Các quyết định chốt mâu thuẫn trong SRS — **thắng SRS** |
| [`docs/traceability.md`](docs/traceability.md) | FR/NFR → endpoint → file → test → trạng thái |
| [`docs/adr/`](docs/adr/) | Architecture Decision Records |
| [`docs/plans/`](docs/plans/) | Kế hoạch implement từng FR |
| [`docs/permissions.md`](docs/permissions.md) · [`docs/team-assignment.md`](docs/team-assignment.md) | Ma trận phân quyền · phân công nhóm |
| [`CHANGELOG.md`](CHANGELOG.md) | Lịch sử thay đổi (Keep a Changelog + SemVer) |

## Nhóm

| Họ và Tên        | Nguyễn Văn Thuận                                                           | Võ Công Vinh                                                                   | Nguyễn Kim Yến                                                                                                                                                                                         | Nguyễn Đức Tín                                                                 |
| ---------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------ |
| MSSV             | 2312765                                                                    | 2312806                                                                        | 2312806                                                                                                                                                                                                | 2312774                                                                        |
| ---              | ---                                                                        | ---                                                                            | ---                                                                                                                                                                                                    | ---                                                                            |
| Email            | <2312765@dlu.edu.vn>                                                       | <2312800@dlu.edu.vn>                                                           | <2312806@dlu.edu.vn>                                                                                                                                                                                   | <2312774@dlu.edu.vn>                                                           |
| ---              | ---                                                                        | ---                                                                            | ---                                                                                                                                                                                                    | ---                                                                            |
| Công việc Tuần 2 | FR-RCP-008(Quản lý ảnh công thức),FR-FILE-001(Upload ảnh lên MinIO)        | FR-AUTH-001(Đăng ký tài khoản), FR-AUTH-002(Đăng nhập bằng Email/Mật khẩu)     | FR-CAT-001 (Xem danh sách danh mục), FR-CAT-002(Xem chi tiết danh mục)                                                                                                                                 | FR-RCP-003 (Tạo công thức nấu năng mới)<br><br>FR-RCP-004 (Cập nhật công thức) |
| ---              | ---                                                                        | ---                                                                            | ---                                                                                                                                                                                                    | ---                                                                            |
| Tiến độ          | 100%                                                                       | 100%                                                                           | 100%                                                                                                                                                                                                   | 0%                                                                             |
| ---              | ---                                                                        | ---                                                                            | ---                                                                                                                                                                                                    | ---                                                                            |
| Công việc Tuần 3 | FR-OBS-001/002 (Module quan sát hệ thống) + Giao diện cho công việc tuần 2 | Giao diện cho công việc tuần 2 + FR-AUTH-003 (Đăng nhập bằng Google OAuth 2.0) | Giao diện cho công việc tuần 2 + FR-CAT-003(Tạo danh mục mới Admin)<br><br>FR-CAT-004(Cập nhật danh mục Admin)<br><br>FR--CAT-005(Xóa danh mục Admin)<br><br>FR-SRCH-001/002/003/004( Module tìm kiếm) |                                                                                |
| ---              | ---                                                                        | ---                                                                            | ---                                                                                                                                                                                                    | ---                                                                            |
| Tiến độ          | FR-OBS-001 (100%)<br><br>FR-OBS-002(70%)                                   | 100%                                                                           | Chưa review                                                                                                                                                                                            |                                                                                |
| ---              | ---                                                                        | ---                                                                            | ---                                                                                                                                                                                                    | ---                                                                            |
| Công việc tuần 4 |                                                                            |                                                                                |                                                                                                                                                                                                        |                                                                                |
| ---              | ---                                                                        | ---                                                                            | ---                                                                                                                                                                                                    | ---                                                                            |
