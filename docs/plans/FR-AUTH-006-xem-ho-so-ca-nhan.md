# Plan — FR-AUTH-006: Xem hồ sơ cá nhân (backend + frontend)

> Người phụ trách: A (Xác thực & Tài khoản) — 2312800@dlu.edu.vn.
> Kế hoạch viết **trước** khi code (bước Plan, mục 9 `CLAUDE.md`).
> Liên quan: FR-AUTH-007 (cập nhật hồ sơ, cùng endpoint `/auth/me`), D5, D12, D41 (đề xuất).

---

## 1. Phạm vi

| FR | Nội dung | Trạng thái |
|---|---|---|
| FR-AUTH-006 (BE) | `GET /api/v1/auth/me` → 200 `UserProfileDto`; 401 chưa đăng nhập; 404 user đã bị xóa | ⬜ Chưa làm |
| FR-AUTH-006 (FE) | Trang `/dashboard/profile` hiển thị hồ sơ, loading skeleton, trạng thái lỗi | ⬜ Chưa làm |

## 2. Hiện trạng

- **Backend:**
  - `AuthEndpoints.cs` mới có stub `GET /me` gọi `NotImplementedResults.Pending` + `RequireAuthorization()`.
  - `UserProfileDto` đã đúng shape D5 → dùng lại, không tạo DTO mới.
  - `IIdentityService` chỉ có `GetUserForRefreshAsync` (ném 403 khi `IsActive=false`, ghi rõ không check lockout)
    → không dùng được cho `/me`, cần thêm `GetUserByIdAsync`.
  - `ICurrentUser.UserId` đã có.
- **Frontend:**
  - `UserProfile` ở `lib/types.ts` đã đúng D5.
  - Chưa có `getMe`, chưa có trang hồ sơ.
  - `/dashboard/*` là CSR (`useQuery` + `apiClient`); `apiClient` tự gắn Bearer từ session.

## 3. Quyết định áp dụng

| Quyết định | Áp dụng |
|---|---|
| D5, D12 | Response chỉ `{ id, email, displayName, avatarUrl, bio, roles }`. Không có `fullName`, `userName`, `emailConfirmed`, `createdAt` (SRS ghi các trường này nhưng D5/D12 loại bỏ). |
| D4 | Không dùng 422; lỗi theo RFC 7807. |
| NFR-SEC-006 | Chỉ đọc hồ sơ của `currentUser.UserId`, không nhận `id` từ client. |
| CONS-002 / CONS-008 | 1 Query + 1 Handler riêng; Query không tham số nên không cần Validator. |
| D8 | Không cache (không `ICacheable`) — dữ liệu theo từng user, không có key trong bảng D8. |
| **D41 (đề xuất — thêm vào `docs/decisions.md` trước khi code)** | Xem dưới. |

### D41 (đề xuất)

SRS FR-AUTH-006 A1 ghi user đã bị xóa → 404, nhưng D35 chốt cho `/refresh` là 401 `AUTH_TOKEN_INVALID`,
và bảng mã lỗi chưa có mã 404 cho user. SRS cũng không nói user `IsActive=false` gọi `/me`.
Chốt:

1. User đã bị xóa khỏi DB sau khi token được cấp → **404** `USER_NOT_FOUND` (giữ nguyên SRS A1).
   Khác `/refresh` vì `/me` đã qua JWT hợp lệ, còn `/refresh` xác thực bằng chính refresh token.
2. User `IsActive=false` còn access token → **200** (chỉ đọc; chặn đã làm ở login và refresh theo D11).
3. Thêm `USER_NOT_FOUND` (404) vào bảng mã lỗi cuối `decisions.md`.

**Sửa SRS:** FR-AUTH-006 — thay `fullName/userName/emailConfirmed/createdAt` theo D5/D12; ghi mã lỗi 404.

## 4. Danh sách file

### Docs
| File | Việc |
|---|---|
| `docs/decisions.md` | Thêm D41; thêm `USER_NOT_FOUND` vào bảng mã lỗi |
| `docs/traceability.md` | Cập nhật dòng FR-AUTH-006 (file, test, D41, trạng thái) — bước commit |
| `docs/plans/FR-AUTH-006-xem-ho-so-ca-nhan.md` | File này |

### Backend

| Tầng | File | Việc |
|---|---|---|
| Domain | (không đổi) | Không có entity / business rule mới |
| Application | `Common/Exceptions/ErrorCodes.cs` | Thêm `UserNotFound = "USER_NOT_FOUND"` // 404 |
| Application | `Common/Interfaces/IIdentityService.cs` | Thêm `GetUserByIdAsync(userId, ct)` → `AuthenticatedUser?`; không ném lỗi inactive, không check lockout |
| Application | `Auth/Queries/GetCurrentUser/GetCurrentUserQuery.cs` | `record : IRequest<UserProfileDto>`, không tham số |
| Application | `Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs` | `currentUser.UserId` null → `UnauthorizedException(AuthTokenInvalid)`; `GetUserByIdAsync` null → `NotFoundException(UserNotFound)`; ngược lại map `UserProfileDto` |
| Infrastructure | `Identity/IdentityService.cs` | Hiện thực `GetUserByIdAsync` bằng `FindByIdAsync` + `GetRolesAsync` |
| API | `Endpoints/AuthEndpoints.cs` | Thay stub bằng `GetMeAsync`: `sender.Send(new GetCurrentUserQuery())` → `TypedResults.Ok`; thêm `.Produces<UserProfileDto>(200)`, `ProducesProblem` 401/404; giữ `RequireAuthorization()` |

`GlobalExceptionMiddleware` không cần sửa (`NotFoundException` đã map 404).

### Backend tests (viết trước, chạy phải fail)

| File | Test |
|---|---|
| `tests/CulinaryBlog.IntegrationTests/Auth/ProfileTests.cs` | `DisplayName` có `FR-AUTH-006/D5`… |
| | Happy path: login rồi `GET /me` với Bearer → 200, đúng 6 trường D5 |
| | D5/D12: JSON không có `fullName`, `userName`, `emailConfirmed`, `createdAt`, `passwordHash`, `securityStamp` |
| | Roles: user mới `["Author"]`; admin seed `["Admin"]` |
| | Không token → 401; token sai chữ ký → 401 |
| | User bị xóa khỏi DB còn token → 404 `USER_NOT_FOUND` |
| | User `IsActive=false` còn token → 200 (D41-2) |
| `tests/CulinaryBlog.UnitTests/Auth/GetCurrentUserQueryHandlerTests.cs` | Tìm thấy → map đúng DTO; không thấy → `NotFoundException`; `UserId` null → `UnauthorizedException` |

### Frontend

| File | Việc |
|---|---|
| `frontend/lib/auth/api.ts` | Thêm `getMe(): Promise<UserProfile>` gọi `GET /auth/me` |
| `frontend/lib/hooks/useCurrentUser.ts` | `useQuery(['auth','me'], getMe)` |
| `frontend/app/dashboard/profile/page.tsx` | `'use client'` (CSR, SRS 5.1). Hiển thị avatar (fallback chữ cái đầu), `displayName`, `email`, `bio` (null → "Chưa có giới thiệu"), badge role. `Skeleton` khi loading; lỗi có nút "Thử lại"; 401 → về trang đăng nhập. Semantic HTML + `aria-label` (WCAG 2.1 AA) |
| Menu điều hướng dashboard | Thêm liên kết "Hồ sơ" (xác định vị trí menu khi vào bước code) |
| `frontend/__tests__/lib/auth/api.test.ts` | Thêm case `getMe`: đúng URL, đúng shape |
| `frontend/__tests__/app/dashboard/profile/page.test.tsx` | Skeleton khi loading; hiển thị đủ trường; bio null có fallback; lỗi có nút thử lại |

Không có toast: đây là thao tác đọc (NFR-USE-004 áp dụng cho write, sẽ làm ở FR-AUTH-007).

## 5. Thứ tự thực hiện

1. Thêm D41 vào `decisions.md`; thêm `USER_NOT_FOUND` vào bảng mã lỗi.
2. **Test first:** viết test BE + FE ở trên, chạy, xác nhận fail.
3. Code: Application → Infrastructure → API → Frontend.
4. Chạy `dotnet build` (0 warning), `dotnet test`, `npm run lint`, `npm run build`, jest.
5. Cập nhật `traceability.md`, commit: `feat(FR-AUTH-006): endpoint xem hồ sơ cá nhân + trang dashboard/profile`.

## 6. Ngoài phạm vi

- Sửa hồ sơ (FR-AUTH-007).
- `emailConfirmed` (D12).
- Cache.
- `GET /users/{id}` (D11).
