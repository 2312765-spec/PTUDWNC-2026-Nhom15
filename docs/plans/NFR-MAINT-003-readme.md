# Plan — NFR-MAINT-003: README setup < 5 phút

> 2026-11-08 · Người làm: C · Nhánh `docs/NFR-MAINT-003-readme`

## Yêu cầu (SRS NFR-MAINT-003)

README hướng dẫn setup dev bằng Docker Compose trong < 5 phút · API docs tự sinh + Scalar `/scalar` · ADR · CHANGELOG
(Keep a Changelog + SemVer).

## Hiện trạng

| Mục | Trạng thái |
|---|---|
| README | ❌ chỉ có bảng thành viên |
| Scalar `/scalar` | ✅ 302 → `/scalar/v1` 200 |
| ADR | ✅ `docs/adr/` 4 ADR + README |
| CHANGELOG | ✅ có (D cập nhật — tuần 8) |

Phát hiện: backend dev **không cần User Secrets** (`appsettings.Development.json` có khóa JWT dev, connection string, S3,
SMTP); Development tự migrate + seed (50 recipe, 5 author, 1 admin). Frontend chỉ bắt buộc `AUTH_SECRET`.
Không có mâu thuẫn SRS → không cần D mới.

## Nội dung README

1. Giới thiệu + tech stack.
2. Yêu cầu cài sẵn (Docker Desktop, .NET 10 SDK, Node ≥ 20.9, Git; gitleaks tùy chọn) + lệnh kiểm version.
3. Chạy trong 5 phút — clone → `docker compose up -d` → `dotnet run` API → `.env.local` + `AUTH_SECRET` →
   `npm ci && npm run dev`.
4. Đăng nhập thử bằng tài khoản seed; bảng cổng/URL (web, API, `/scalar`, Seq, Mailhog, `/health`).
5. Chạy test (`dotnet test`, `npm test`, `npm run test:e2e -- --workers=1`, `scripts/verify.sh`).
6. Sự cố thường gặp: 404 `/api/auth/*` (xóa `frontend/.next`), Docker tắt, cổng bị chiếm, E2E song song.
7. Tài liệu khác: `CLAUDE.md`, `docs/`, `CHANGELOG.md`.
8. Nhóm: giữ bảng thành viên hiện có.

## Kiểm chứng

Clone repo ra thư mục tạm, làm đúng theo README, bấm giờ (không tính tải Docker image lần đầu và cài SDK). Ghi kết
quả vào `traceability.md`; đạt → NFR-MAINT-003 ✅.
