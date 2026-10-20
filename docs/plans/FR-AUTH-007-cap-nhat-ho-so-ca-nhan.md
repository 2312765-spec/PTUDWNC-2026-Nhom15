# Kế hoạch FR-AUTH-007 — Cập nhật hồ sơ cá nhân

> Endpoint: `PATCH /api/v1/auth/me` · Ưu tiên: S (Should Have) · Tác nhân: Author/Admin đang đăng nhập
> Liên quan: FR-AUTH-006 (xem hồ sơ, cùng endpoint `/auth/me`), D4, D5, D12, D23, D30, D48, D54 (đề xuất).

## Hiện trạng

- `AuthEndpoints.cs:68` chỉ là stub `NotImplementedResults.Pending("FR-AUTH-007", "A")`.
- Đường đọc đã có: `GetCurrentUserQueryHandler`, `IIdentityService.GetUserByIdAsync`, `useCurrentUser`, trang `/dashboard/profile`.

## Quyết định áp dụng

| Quyết định | Nội dung áp dụng cho FR này |
|---|---|
| D5 | Body `{ displayName?, avatarUrl?, bio? }`. Response 6 trường `{ id, email, displayName, avatarUrl, bio, roles }`. Không có `fullName`, `userName`. `displayName` 2–100 ký tự, `avatarUrl` URL hợp lệ nếu có, `bio` ≤ 1000. |
| D4 | Dữ liệu sai → **400** `VALIDATION_ERROR` (SRS A1 ghi 422 là sai). |
| D12 | Không trả `emailConfirmed`. |
| D23 | Đi qua `IIdentityService`, Application không biết `ApplicationUser`. |
| D30 | Giữ ErrorCode riêng của từng rule FluentValidation. |
| D48 | User đã bị xóa → 404 `USER_NOT_FOUND`. |

### D54 (đề xuất, ghi vào `decisions.md` trước khi code)

SRS và D1–D50 chưa phủ các tình huống sau.

| # | Tình huống | Chốt |
|---|---|---|
| 1 | Body `{}` hoặc không có field nào | **400** `VALIDATION_ERROR` (cùng tinh thần D27) |
| 2 | Field vắng hoặc `null` | **Giữ nguyên.** Xóa `bio`/`avatarUrl` bằng chuỗi rỗng `""`, server lưu `NULL`. |
| 3 | `displayName` là `""` hoặc chỉ khoảng trắng | 400 (D5: NOT NULL, 2–100 ký tự) |
| 4 | User `IsActive=false` còn access token | **200**, giống D48 (không cấp thêm quyền, D11 chặn ở login/refresh) |
| 5 | User đã bị xóa | **404** `USER_NOT_FOUND` (D48) |

Lý do mục 2: record C# với `string?` không phân biệt được "vắng" và "null"; muốn phân biệt phải tự dò JSON, không cần cho v1.

## Backend

### Application
| File | Việc |
|---|---|
| `Auth/Commands/UpdateProfile/UpdateProfileCommand.cs` (tạo) | `record(string? DisplayName, string? AvatarUrl, string? Bio) : IRequest<UserProfileDto>`. Không `ICacheInvalidator` (không có cache user). |
| `Auth/Commands/UpdateProfile/UpdateProfileCommandValidator.cs` (tạo) | Ít nhất 1 field có mặt. `displayName` (nếu có) trim rồi 2–100 ký tự. `avatarUrl` (nếu không rỗng) là URL tuyệt đối http/https, ≤ 500 ký tự. `bio` ≤ 1000. Mỗi rule có ErrorCode riêng. |
| `Auth/Commands/UpdateProfile/UpdateProfileCommandHandler.cs` (tạo) | `ICurrentUser.UserId` (null → `UnauthorizedException`) → `identityService.UpdateProfileAsync` → null thì `NotFoundException(USER_NOT_FOUND)` → map `UserProfileDto`. |
| `Common/Interfaces/IIdentityService.cs` (sửa) | Thêm `Task<AuthenticatedUser?> UpdateProfileAsync(string userId, string? displayName, string? avatarUrl, string? bio, CancellationToken)`. User không tồn tại → `null`. |

### Infrastructure
| File | Việc |
|---|---|
| `Identity/IdentityService.cs` (sửa) | Hiện thực `UpdateProfileAsync`: `FindByIdAsync`, chỉ gán field khác null, `""` thành `NULL` cho `avatarUrl`/`bio`, `UserManager.UpdateAsync`. Thất bại → `EnsureSucceeded` (500). Đọc lại roles. |

Không cần migration (không đổi schema). **Kiểm tra** độ dài cột `AvatarUrl` trong migration `InitialCreate`; nếu ngắn hơn 500 thì validator phải khớp cột.

### API
| File | Việc |
|---|---|
| `Endpoints/AuthEndpoints.cs` (sửa) | Thay stub bằng `UpdateMeAsync(UpdateProfileRequest, ISender, ct)` chỉ `sender.Send` rồi `TypedResults.Ok`. Thêm `.Produces<UserProfileDto>(200)`, `.ProducesValidationProblem()`, 401, 404, `.RequireAuthorization()`. Thêm `public sealed record UpdateProfileRequest(string? DisplayName, string? AvatarUrl, string? Bio)`. |

## Test (viết trước, xác nhận fail)

Tên test ghi rõ FR/D, theo mẫu `ProfileTests`.

**Integration** — `IntegrationTests/Auth/UpdateProfileTests.cs` (tạo, dùng `PostgresApiFactory`; file riêng để tránh xung đột merge với `ProfileTests.cs`):
- Happy path: đổi `displayName` → 200, `GET /me` thấy giá trị mới.
- Partial: chỉ gửi `bio` thì `displayName` giữ nguyên.
- `""` xóa `bio` và `avatarUrl` → response `null`.
- Response đúng 6 trường; không có `fullName`, `userName`, `emailConfirmed`, `passwordHash` (D5, D12).
- `email` trong body bị bỏ qua, email không đổi.
- Body rỗng → 400. `displayName` 1 hoặc 101 ký tự → 400. `avatarUrl` không phải URL (kể cả `javascript:`) → 400. `bio` 1001 ký tự → 400. Tất cả `VALIDATION_ERROR`, không có 422.
- Không có token → 401.
- User bị xóa → 404 `USER_NOT_FOUND`.
- `IsActive=false` → 200.
- Cách ly: user A sửa không ảnh hưởng user B.

**Unit** — `UnitTests/Auth/UpdateProfileCommandValidatorTests.cs`, `UpdateProfileCommandHandlerTests.cs` (tạo; mock `IIdentityService`, `ICurrentUser`). Giữ coverage Application ≥ 80%.

## Frontend

| File | Việc |
|---|---|
| `lib/types.ts` (sửa) | Thêm `UpdateProfileRequest` nếu `UserProfile` chưa đủ. |
| `lib/auth/api.ts` (sửa) | Thêm `updateMe(body): Promise<UserProfile>` gọi `PATCH /auth/me`. |
| `lib/auth/schemas.ts` (sửa) | Thêm Zod `profileSchema`, khớp ràng buộc backend. |
| `lib/hooks/useUpdateProfile.ts` (tạo) | `useMutation`; thành công thì `setQueryData(currentUserQueryKey, data)` + toast (NFR-USE-004). |
| `app/dashboard/profile/EditProfileForm.tsx` (tạo) | Client component, React Hook Form + Zod. 3 field, nút lưu có trạng thái đang gửi, lỗi 400 hiện theo field, `aria-label`. |
| `app/dashboard/profile/page.tsx` (sửa) | Thêm nút "Chỉnh sửa" mở form (inline hoặc `Dialog` có sẵn). |
| `__tests__/lib/hooks/useUpdateProfile.test.tsx`, `__tests__/app/dashboard/profile/EditProfileForm.test.tsx` (tạo) | Lỗi validate, gửi đúng body, toast khi thành công, trạng thái đang lưu. |

**Rủi ro cần kiểm tra khi code:** nếu `HeaderAuth`/`SiteHeader` lấy tên từ session Auth.js thì sau khi sửa `displayName`, tên ở header sẽ cũ → cần `session.update()` hoặc đọc tên từ `useCurrentUser`. Đọc `HeaderAuth.tsx` ở bước code.

## Tài liệu

- `docs/decisions.md`: thêm D54.
- `docs/traceability.md`: dòng FR-AUTH-007 ⬜ → ✅ kèm file và test.
- `docs/permissions.md`: kiểm tra có dòng `PATCH /auth/me` chưa, chưa có thì thêm.
- `backend/CulinaryBlog.Application/Auth/OWNER.md`: cập nhật nếu có liệt kê file.

## Thứ tự làm

1. Lưu plan, ghi D54.
2. Viết integration + unit test, chạy, xác nhận fail.
3. Backend: interface → validator → handler → `IdentityService` → endpoint, đến khi pass. `dotnet build` 0 warning, `dotnet test`.
4. Frontend: api → schema → hook → form → page. `npm run lint && npm run build`, Jest.
5. Cập nhật `traceability.md`, commit `feat(FR-AUTH-007): ...` (backend và frontend tách commit).
