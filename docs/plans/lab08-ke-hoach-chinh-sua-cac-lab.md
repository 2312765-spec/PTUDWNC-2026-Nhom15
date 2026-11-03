# Plan — lab08: Chỉnh sửa phần việc của Vinh (mảng A — Xác thực & Tài khoản)

> Phạm vi: FR-AUTH-001 → 007, FR-JOB-001 và phần bảo mật S12 của mảng A.
> Nguồn: `docs/team-assignment.md` (mảng A), `docs/traceability.md`, `docs/decisions.md` (D15), `docs/permissions.md`.
> Trạng thái (2026-11-03): **Việc 1–5 đã làm** (xem mục "Kết quả" cuối file). Giả định Vinh = mảng A (các nhánh `2312800_VCVinh_FR-AUTH-*`).

## Hiện trạng

| FR | Trạng thái trong `traceability.md` |
|---|---|
| FR-AUTH-001, 002, 003, 004 | ✅ (E2E Playwright chưa có) |
| FR-AUTH-005 | ✅ |
| FR-AUTH-006 | 🟡 backend chưa xác nhận test pass |
| FR-AUTH-007 | ✅ |
| FR-JOB-001 | ✅ |

Các handler của mảng A đều có trong `backend/`. Trang hồ sơ nằm ở `frontend/app/dashboard/profile/` (`page.tsx`, `EditProfileForm.tsx`).

## Việc 1. Chạy test để chốt FR-AUTH-006
- Máy có `dotnet` (`C:\Program Files\dotnet\`), chạy được `dotnet test`.
- Chạy `dotnet test --filter "FullyQualifiedName~Auth"`, riêng `ProfileTests` và `GetCurrentUserQueryHandlerTests`; chạy `dotnet build` xác nhận 0 warning.
- Pass → đổi 🟡 thành ✅ ở FR-AUTH-006, bỏ ghi chú "thiếu .NET 10 SDK" ở FR-AUTH-005 và 006.
- Fail → sửa theo lỗi thật, không đoán.

## Việc 2. Chặn route chưa đăng nhập
- **Hiện trạng:** `frontend/proxy.ts` chỉ refresh token, không chặn route. `lib/auth/callbacks.ts` không có logic `authorized`. Không thấy trang `/dashboard` nào redirect khi chưa đăng nhập.
- **Cách làm:**
  - Thêm callback `authorized` trong cấu hình Auth.js.
  - Chưa đăng nhập vào `/dashboard/*` → redirect `/auth/login?callbackUrl=...`, dùng lại `lib/auth/callback-url.ts` để chống open redirect.
  - Giữ nguyên việc refresh trong `proxy.ts`.
- **Ranh giới:** chỉ kiểm tra "đã đăng nhập chưa". Kiểm tra role (Author/Admin/Owner) thuộc mảng B và C; backend vẫn là nơi chặn thật (`permissions.md` mục 4).
- **Cần chốt:** `permissions.md` và `team-assignment.md` ghi route `/profile`, code đang ở `/dashboard/profile`.
  - (a) Giữ `/dashboard/profile`, sửa hai file tài liệu. **Đề xuất**, vì SRS 5.1 ghi `/dashboard/*` là CSR.
  - (b) Chuyển sang `/profile`.
  - Cần một mục D-mới trong `docs/decisions.md`, không tự quyết im lặng.
- **Test:** Jest cho callback `authorized`; một E2E "vào `/dashboard` khi chưa đăng nhập".

## Việc 3. Rate limiting (D15, NFR-SEC-003)
- **Hiện trạng:** backend chưa có `AddRateLimiter`/`UseRateLimiter`; chỉ có `RATE_LIMIT_EXCEEDED` trong `ErrorCodes.cs`.
- **Cách làm (chỉ ở tầng API):**
  - Ba policy theo IP: `/auth/*` 10 req/phút, API chung 100, upload 5.
  - Vượt → 429 RFC 7807, `type` = `RATE_LIMIT_EXCEEDED`, kèm `Retry-After` và `X-RateLimit-Limit/Remaining/Reset`.
  - Gắn vào `AuthEndpoints` bằng `.RequireRateLimiting(...)`.
  - Đặt middleware sau `CorrelationId` để log có `CorrelationId` (CONS-010).
- **Rủi ro:**
  - SRS vừa ghi "sliding window cho auth" vừa ghi Fixed Window. Kiểm tra D15 đã chốt chưa; chưa thì thêm D-mới.
  - Integration test gửi nhiều request sẽ chạm giới hạn lẫn nhau → dùng cấu hình giới hạn riêng cho test hoặc reset giữa các test.
- **Test:** integration (request thứ 11 vào `/auth/login` trả 429 kèm `Retry-After`) cho mỗi nhóm policy.

## Việc 4. E2E Playwright cho register và login
- **Hiện trạng:** `frontend/e2e/` chỉ có `recipe.spec.ts`, `search.spec.ts`, `global-setup.ts`.
- **Thêm `auth.spec.ts`:**
  - Register với email ngẫu nhiên → về `/dashboard`.
  - Login thành công; login sai mật khẩu → message generic (không lộ email có tồn tại hay không).
  - Chưa đăng nhập vào `/dashboard` → chuyển về login (từ Việc 2).
- **Phụ thuộc:** làm sau Việc 2. Rate limit 10 req/phút có thể làm E2E flaky → cần nới/tắt trong môi trường E2E (xem Việc 3).

## Việc 5. Phần S12 còn lại
- **CORS, HSTS:** `Program.cs` đã có `UseCors` và `UseHsts`. Cần đọc để xác nhận allowlist không dùng `*` và `nginx/nginx.conf` có HSTS. **Chưa đọc hai file này.**
- **Secrets:** chưa có `.gitleaks.toml` hay `.githooks`. Phân công yêu cầu "secrets + gitleaks hook"; thay đổi quy trình commit của cả nhóm nên cần cả nhóm xác nhận.

## Thứ tự và commit
1 → 2 → 3 → 4 → 5, mỗi việc một commit riêng, cập nhật `traceability.md` sau mỗi việc:

| Việc | Commit |
|---|---|
| 1 | `test(FR-AUTH-006): ...` / `docs: ...` |
| 2 | `feat(FR-AUTH-006): chặn route /dashboard khi chưa đăng nhập` |
| 3 | `feat(NFR-SEC-003): thêm rate limiting` |
| 4 | `test(FR-AUTH-001): ...`, `test(FR-AUTH-002): ...` |
| 5 | `chore(NFR-SEC-005)` / `chore(NFR-SEC-007)` |

Việc 2 và 3 theo quy trình CLAUDE.md: viết test fail trước, rồi mới code.

## Điều kiện trước khi bắt đầu
- Cài Git (máy hiện không có `git` trong PATH), pull `main` về trước khi làm.
- Chốt route profile (a hay b) và xác nhận làm gitleaks hay không.

## Kết quả (2026-11-03)

| Việc | Kết quả |
|---|---|
| 1 | Unit Auth 97/97, integration Logout+Profile 55/55, build 0 warning. FR-AUTH-006 → ✅ |
| 2 | Chốt route `/dashboard/profile` (D58). `authorizedCallback` chặn `/dashboard/*`; Jest 361/361, `tsc` + lint sạch |
| 3 | `RateLimitingMiddleware` theo D15; `RateLimitingTests` 11/11. Thêm `UseForwardedHeaders` để đếm theo IP thật sau Nginx |
| 4 | `e2e/auth.spec.ts` 5/5 pass trên stack thật (register, login, sai mật khẩu, 2 ca chặn route D58) |
| 5 | nginx HTTPS + HSTS (kiểm bằng container), `CorsTests` 6/6, `.gitleaks.toml` + `.githooks/pre-commit` |

Toàn bộ backend: unit 284, kiến trúc 10 (1 skip có sẵn), integration 275 — đều pass.

**Còn treo:** chứng chỉ TLS thật + `docker-compose.prod.yml`; chạy hook gitleaks thật khi có git; bước CI quét secret;
`Jwt:Key` mặc định nằm trong `appsettings.json`/`Program.cs`; request từ Next.js server tới API (`API_INTERNAL_URL`)
chưa chuyển tiếp IP người dùng nên bị tính chung một IP (xem báo cáo).