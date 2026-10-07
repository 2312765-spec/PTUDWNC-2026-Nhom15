# Plan — FR-AUTH-005: Đăng xuất / thu hồi refresh token (backend + frontend)

> Người phụ trách: A (Xác thực & Tài khoản) — 2312800@dlu.edu.vn.
> Kế hoạch viết **trước** khi code (bước Plan, mục 9 `CLAUDE.md`).
> Liên quan: FR-AUTH-004 (`docs/plans/FR-AUTH-004-lam-moi-token.md`, `FR-AUTH-004-frontend-tu-refresh.md`), D20, D35.

---

## 1. Phạm vi

| FR | Nội dung | Trạng thái |
|---|---|---|
| FR-AUTH-005 (BE) | `POST /api/v1/auth/logout` → 204; revoke refresh token của user hiện tại, idempotent | ⬜ Chưa làm |
| FR-AUTH-005 (FE) | Auth.js `signOut` gọi `/auth/logout`; nút đăng xuất; toast xác nhận | ⬜ Chưa làm |

## 2. Hiện trạng

- **Backend:** `AuthEndpoints.cs:52` mới có stub `NotImplementedResults.Pending` + `RequireAuthorization()`.
  Chưa có `LogoutCommand`. Tái sử dụng được: `IRefreshTokenRepository.GetByTokenAsync` /
  `TryRevokeAsync` (D35-6), `IJwtService.HashToken`, `ICurrentUser`.
- **Frontend:** còn `TODO` ở `auth.ts:17`. Chưa có nút đăng xuất, chưa có header nào trong app.
- **Phiên Auth.js:** refresh token nằm trong JWT cookie httpOnly → chỉ server đọc được. Vì vậy
  phải gọi logout trong `events.signOut` của Auth.js, không gọi từ trình duyệt.

## 3. Quyết định áp dụng

| Quyết định | Áp dụng |
|---|---|
| D20 | `RevokedAt` (không có cột `IsRevoked`); tra theo `TokenHash` SHA-256 |
| D35-6 | Revoke bằng UPDATE có điều kiện `RevokedAt IS NULL` (`TryRevokeAsync`) |
| D4 | Body thiếu `refreshToken` → 400 `VALIDATION_ERROR`, không dùng 422 |
| CONS-008 | Validate bằng `LogoutCommandValidator` trong ValidationBehavior |
| NFR-SEC-006 | Kiểm tra "token thuộc user hiện tại" ở Application layer (handler) |

### D40 (đề xuất — **thêm vào `docs/decisions.md` trước khi code**)

SRS FR-AUTH-005 mơ hồ ở A2 (access token hết hạn vẫn logout vs. điều kiện tiên quyết cần access
token hợp lệ) và không nói token của user khác. Chốt:

1. **Giữ `RequireAuthorization()`** (khớp SRS bảng status, traceability, stub hiện có). Bỏ A2.
   Frontend refresh trước nếu access token sắp hết hạn rồi mới logout.
   Body thiếu `refreshToken` → 400 `VALIDATION_ERROR` (thay cho 401 của A2).
2. **Token thuộc user khác** → 204, **không revoke**, log WARNING (giống A1, không lộ trạng thái).
3. **Token đã revoke / hết hạn** → 204, **không** kích hoạt reuse detection D35-5 (chỉ dành cho `/refresh`).
4. **Race** → dùng `TryRevokeAsync`; thua race vẫn 204.

**Sửa SRS:** FR-AUTH-005 — bỏ A2, thêm 400 vào bảng status; ghi rõ token user khác → 204.

## 4. Backend

| Tầng | Việc | File |
|---|---|---|
| Domain | **Sửa** `TryRevokeAsync(tokenHash, string? replacedByTokenHash)` cho phép `null` | `Domain/Interfaces/IRefreshTokenRepository.cs` |
| Application | **Tạo** `LogoutCommand(string RefreshToken) : IRequest` | `Application/Auth/Commands/Logout/LogoutCommand.cs` |
| Application | **Tạo** validator (`NotEmpty`, độ dài tối đa hợp lý) | `.../Logout/LogoutCommandValidator.cs` |
| Application | **Tạo** handler: hash → tìm → bỏ qua nếu không có / khác `ICurrentUser.UserId` / đã revoke → ngược lại `TryRevokeAsync` | `.../Logout/LogoutCommandHandler.cs` |
| Infrastructure | **Sửa** `TryRevokeAsync` nhận `replacedByTokenHash` nullable | `Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs` |
| API | **Sửa** thay stub bằng `LogoutAsync` (`LogoutRequest` → `sender.Send` → `TypedResults.NoContent()`); `Produces 204/401`, `ProducesValidationProblem`; thêm `record LogoutRequest(string RefreshToken)` | `API/Endpoints/AuthEndpoints.cs` |

Không cần migration, không cần mã lỗi mới.

### Test backend (viết trước, mỗi test ghi rõ FR-AUTH-005)

| File | Ca kiểm tra |
|---|---|
| `UnitTests/Auth/LogoutCommandHandlerTests.cs` (mới) | token hợp lệ → revoke · không tìm thấy → no-op · token user khác → no-op · đã revoke → no-op · thua race → không ném |
| `UnitTests/Auth/LogoutCommandValidatorTests.cs` (mới) | rỗng / quá dài → lỗi |
| `IntegrationTests/Auth/LogoutTests.cs` (mới) | happy path 204, rồi `/refresh` bằng token đó → 401 `AUTH_REFRESH_TOKEN_REVOKED` · không Bearer → 401 · token không tồn tại → 204 · token user khác → 204 và token đó vẫn refresh được · logout hai lần → đều 204 · body thiếu `refreshToken` → 400 `VALIDATION_ERROR` |

## 5. Frontend

| Việc | File |
|---|---|
| **Sửa** thêm `logout(refreshToken, accessToken)` gọi `POST /auth/logout` kèm Bearer, dùng `serverBaseUrl()` | `frontend/lib/auth/api.ts` |
| **Tạo** `revokeRefreshToken(token: JWT)` best-effort: không có RT → bỏ qua; access token sắp hết hạn → `refreshAccessToken` trước; lỗi mạng/5xx chỉ log, **không ném** (user luôn phải đăng xuất được ở phía client) | `frontend/lib/auth/logout.ts` |
| **Sửa** thêm `events: { signOut }` gọi `revokeRefreshToken(token)`; xóa TODO FR-AUTH-005 | `frontend/auth.ts` |
| **Tạo** `LogoutButton` (`'use client'`): `signOut({ redirectTo: '/auth/login?loggedOut=1' })`, trạng thái pending, `aria-label`, điều hướng bàn phím | `frontend/components/auth/LogoutButton.tsx` |
| **Sửa** hiện toast "Đã đăng xuất" khi có `?loggedOut=1` (NFR-USE-004; dùng query param vì redirect làm mất state toast). Đọc kỹ cách form xử lý `?error=` trước khi sửa | `frontend/app/auth/login/LoginForm.tsx` |
| **Tạo** vị trí gắn nút — xem câu hỏi mở §7 | `frontend/app/dashboard/layout.tsx` (đề xuất) |

**Lưu ý:** `SessionExpiryWatcher` (FR-AUTH-004) cũng gọi `signOut`, nhưng lúc đó `refreshToken` đã
bị xóa khỏi token → `revokeRefreshToken` bỏ qua, không gọi backend thừa.

### Test frontend (Jest + Testing Library)

| File | Ca kiểm tra |
|---|---|
| `__tests__/lib/auth/api.test.ts` (sửa) | `logout()` gửi đúng URL, body, header Bearer |
| `__tests__/lib/auth/logout.test.ts` (mới) | có token → gọi API · không có RT → không gọi · sắp hết hạn → refresh trước rồi logout · lỗi mạng/5xx → không ném |
| `__tests__/components/auth/LogoutButton.test.tsx` (mới) | bấm → `signOut` đúng `redirectTo` · trạng thái pending · a11y |
| `__tests__/app/auth/LoginForm.test.tsx` (sửa/mới) | `?loggedOut=1` → hiện toast |

## 6. Docs

- `docs/decisions.md` — thêm D40 + dòng vào bảng đầu file.
- `docs/traceability.md` — dòng FR-AUTH-005 → ✅, ghi thêm phần frontend.
- `docs/permissions.md` — không cần sửa.

## 7. Câu hỏi mở / ngoài phạm vi

- **Chỗ đặt nút đăng xuất:** app chưa có header. Đề xuất `app/dashboard/layout.tsx` tối giản
  (tên user + `LogoutButton`). Nếu người khác đang làm header thì chỉ làm component.
- **Ngoài phạm vi:** "đăng xuất mọi thiết bị"; header/user menu hoàn chỉnh; E2E Playwright
  (chưa có thư mục `e2e/`).

## 8. Thứ tự thực hiện

1. Kéo `main` mới nhất về; thêm D40 vào `decisions.md`.
2. Viết test backend + frontend (mục 4, 5) → chạy, xác nhận **fail**.
3. Code backend rồi frontend đến khi test pass; `dotnet build` 0 warning, `npm run lint && npm run build` sạch.
4. Cập nhật `traceability.md` → commit `feat(FR-AUTH-005): ...` trên nhánh `feature/FR-AUTH-005-logout`.
