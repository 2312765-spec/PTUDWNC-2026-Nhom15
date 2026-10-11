# Plan — FR-AUTH-004: Làm mới Access Token (Token Refresh)

> Người phụ trách: A (Xác thực & Tài khoản) — 2312800@dlu.edu.vn.
> Kế hoạch viết **trước** khi code (bước Plan, mục 9 `CLAUDE.md`).

---

## 1. Phạm vi

| FR | Nội dung | Trạng thái |
|---|---|---|
| FR-AUTH-004 | `POST /api/v1/auth/refresh` — đổi refresh token (RT) lấy cặp token mới, Token Rotation + Reuse Detection | 🟡 Đã viết integration test, chưa code |

**Frontend** (Auth.js tự refresh khi access token hết hạn) — đề xuất tách thành bước/PR riêng
sau khi backend merge, xem mục 7.

## 2. Yêu cầu tóm tắt (SRS Chương 3 FR-AUTH-004 + NFR-SEC-002)

- Body `{ "refreshToken": "..." }`, không cần Bearer, RT nằm trong body (không cookie — SRS 8).
- RT hợp lệ → revoke RT cũ, sinh access token (15 phút) + RT mới (7 ngày, 128-bit) → **200**.
- A1 không tìm thấy · A2 hết hạn · A3 đã revoke (reuse — log WARNING, revoke token family) ·
  A4 user bị xóa/khóa → **401**.

## 3. Quyết định D-x áp dụng

| D-x | Tóm tắt | Ảnh hưởng |
|---|---|---|
| D20 | Không có cột `IsRevoked` (computed từ `RevokedAt`), dùng `ReplacedByTokenHash` (không phải `ReplacedByToken`). Hash RT client gửi rồi tra `TokenHash`. Reuse → revoke family, WARNING, 401 `AUTH_REFRESH_TOKEN_REVOKED` | Thay bước 4–5 của SRS |
| D25 | RT mới 128-bit, SHA-256 | Dùng `IJwtService.GenerateRefreshToken()` có sẵn |
| D26 | `RefreshToken` ở Domain, `IRefreshTokenRepository` ở `Domain/Interfaces` | Handler tạo trực tiếp `RefreshToken.Create(...)` |
| D11 | `IsActive == false` → **403** `AUTH_ACCOUNT_DISABLED`, kiểm tra cả ở refresh | Thắng SRS A4 (401) |
| D23 | Application không đụng `ApplicationUser`/`UserManager` | Tra user qua `IIdentityService` |
| D24, D5 | Response = `AuthResponseDto` giống login | Thắng bảng 8.1 (`expiresIn`) |
| D4 | Body rỗng → 400 `VALIDATION_ERROR` | Validator |
| D15 | `/auth/*` 10 req/phút/IP | Ngoài phạm vi FR này (slice S12) |
| **D35** *(cần thêm)* | Xem mục 4 | |

## 4. D35 — các điểm SRS chưa chốt (thêm vào `decisions.md` trước khi code)

| # | Điểm | Chốt đề xuất |
|---|---|---|
| a | A1 (không tìm thấy RT) / A4 (user bị xóa) dùng mã gì | 401 `AUTH_TOKEN_INVALID` |
| b | User đang lockout (sai mật khẩu 5 lần) | **Không kiểm tra khi refresh** — người giữ RT đã chứng minh danh tính; kiểm tra sẽ cho kẻ tấn công đá chủ tài khoản ra bằng cách cố ý nhập sai. "Bị khóa" trong A4 đã được D11 xử lý qua `IsActive` |
| c | "Token family" | Revoke **mọi RT còn hiệu lực của user** — không cần cột `FamilyId`, không migration |
| d | Shape response | `AuthResponseDto` (theo lập luận D24) |
| e | Thứ tự kiểm tra | tồn tại → **revoked** → expired → user (để token vừa revoke vừa hết hạn vẫn kích hoạt reuse detection) |
| f | Hai request refresh đồng thời cùng một RT | Request thắng nhận token mới; request thua → 401 `AUTH_REFRESH_TOKEN_REVOKED` **không** revoke family (tránh đá user vì mở 2 tab) |

**Sửa SRS (qua CR):** FR-AUTH-004 bước 4–5 (tên cột theo D20), A1/A4 (mã lỗi, D11), bảng 8.1
dòng `/auth/refresh` (response = `AuthResponseDto`).

## 5. Luồng handler

1. `hash = jwtService.HashToken(raw)` → `GetByTokenAsync(hash)`.
2. `null` → 401 `AUTH_TOKEN_INVALID`.
3. `IsRevoked` → log **WARNING** (userId, tokenId, IP — không log raw token) →
   `RevokeAllActiveForUserAsync(userId)` → 401 `AUTH_REFRESH_TOKEN_REVOKED`.
4. `IsExpired` → 401 `AUTH_REFRESH_TOKEN_EXPIRED`.
5. `identityService.GetUserForRefreshAsync(userId)`: `null` → 401 `AUTH_TOKEN_INVALID`;
   `IsActive == false` → 403 `AUTH_ACCOUNT_DISABLED`.
6. Sinh access token + RT mới.
7. `TryRevokeAsync(oldHash, newHash)` — UPDATE có điều kiện `RevokedAt IS NULL`;
   0 dòng → thua race → 401 `AUTH_REFRESH_TOKEN_REVOKED` (điểm f).
8. `AddAsync(newToken)` → `SaveChangesAsync` → 200 `AuthResponseDto`.

Bước 3 và 7 dùng `ExecuteUpdateAsync` (LINQ, đúng CONS-006) — commit ngay, nên revoke vẫn giữ
hiệu lực dù handler ném exception sau đó.

## 6. File theo từng tầng

### Domain — `backend/CulinaryBlog.Domain/`
- **Sửa** `Interfaces/IRefreshTokenRepository.cs` — thêm `Task<bool> TryRevokeAsync(string tokenHash, string replacedByTokenHash, CancellationToken)`, `Task<int> RevokeAllActiveForUserAsync(string userId, CancellationToken)`. Đổi tên tham số `GetByTokenAsync(token)` → `tokenHash` (không đổi chữ ký).
- Entity `RefreshToken` giữ nguyên (`IsRevoked`, `IsExpired`, `Revoke()` đã đủ).

### Application — `backend/CulinaryBlog.Application/`
- **Tạo** `Auth/Commands/Refresh/RefreshTokenCommand.cs` — `record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<AuthResponseDto>`.
- **Tạo** `Auth/Commands/Refresh/RefreshTokenCommandHandler.cs` — luồng mục 5, inject `ILogger<>` cho security alert.
- **Tạo** `Auth/Commands/Refresh/RefreshTokenCommandValidator.cs` — `RefreshToken` không rỗng/khoảng trắng, giới hạn độ dài.
- **Sửa** `Common/Interfaces/IIdentityService.cs` — thêm `Task<AuthenticatedUser?> GetUserForRefreshAsync(string userId, CancellationToken)`.

> Thư mục tên `Refresh`, **không** phải `RefreshToken` — nếu không namespace
> `...Commands.RefreshToken` sẽ che kiểu `Domain.Entities.RefreshToken` trong handler.

### Infrastructure — `backend/CulinaryBlog.Infrastructure/`
- **Sửa** `Persistence/Repositories/RefreshTokenRepository.cs` — implement 2 method mới bằng `ExecuteUpdateAsync`.
- **Sửa** `Identity/IdentityService.cs` — `GetUserForRefreshAsync`: `FindByIdAsync` → check `IsActive` → `GetRolesAsync`. Không check lockout (D35-b).
- Không migration.

### API — `backend/CulinaryBlog.API/`
- **Sửa** `Endpoints/AuthEndpoints.cs` — thay stub dòng 43 bằng `RefreshAsync` theo pattern `LoginAsync`; `AllowAnonymous`, `Produces` 200/400/401/403; thêm `record RefreshTokenRequest(string RefreshToken)`.
- `GlobalExceptionMiddleware` không cần sửa (401/403 đã map sẵn).

### Tests
- ✅ **Đã tạo** `tests/CulinaryBlog.IntegrationTests/Auth/RefreshTokenTests.cs` — 15 case: happy path (200, rotation, chỉ lưu hash, refresh nối tiếp, không cần Bearer), A1, A2, A3 (reuse + revoke mọi RT của user + ưu tiên REVOKED), A4 (disabled 403, user bị xóa 401, lockout vẫn refresh được), D4 (400).
- **Tạo** `tests/CulinaryBlog.UnitTests/Auth/RefreshTokenCommandHandlerTests.cs` — mọi nhánh, gồm `TryRevokeAsync` trả `false` (D35-f) và log Warning khi reuse.
- **Tạo** `tests/CulinaryBlog.UnitTests/Auth/RefreshTokenCommandValidatorTests.cs`.

### Docs
- `docs/decisions.md` — thêm **D35**.
- `docs/traceability.md` — cập nhật dòng FR-AUTH-004 + tiến độ.

## 7. Frontend (bước sau, PR riêng — đề xuất)

- **Sửa** `frontend/lib/auth/api.ts` — `refreshTokens(refreshToken)` gọi `POST /api/v1/auth/refresh`.
- **Sửa** `frontend/auth.ts` — xử lý TODO "tự refresh khi accessToken hết hạn" trong callback `jwt`; thất bại → `token.error = 'RefreshTokenError'` để UI buộc đăng nhập lại.
- **Sửa** `frontend/types/next-auth.d.ts` — thêm `accessTokenExpires`, `error`.
- **Tạo** `frontend/__tests__/lib/auth/refresh.test.ts`.
- Trước khi code: đọc `node_modules/next/dist/docs/` (xem `frontend/AGENTS.md`) và các file trên — chưa được khảo sát chi tiết khi lập kế hoạch này.

## 8. Ngoài phạm vi (ghi nhận)

- Rate limit `/auth/*` (D15) — slice S12.
- Bug `RefreshTokenRepository.AddAsync`: trùng hash thì `ReassignTokenHash` gán hash ngẫu nhiên → raw token trả client không bao giờ khớp. Xác suất ~0 với 128-bit nhưng ảnh hưởng cả register/login → PR `fix` riêng.

## 9. Thứ tự thực hiện

1. Thêm D35 vào `decisions.md`.
2. ✅ Viết integration test → chạy, xác nhận **fail** (đang chờ môi trường: .NET 10 SDK + Docker Desktop).
3. Viết unit test.
4. Code: Domain → Infrastructure → Application → API đến khi test pass, `dotnet build` 0 warning.
5. Cập nhật `traceability.md` → commit `feat(FR-AUTH-004): ...` trên nhánh riêng.
6. Frontend (mục 7).
