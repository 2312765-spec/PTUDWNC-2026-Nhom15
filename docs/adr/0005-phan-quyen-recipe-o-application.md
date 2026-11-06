# ADR-0005: Phân quyền resource-based cho Recipe bằng `RecipeAuthorizationHandler` ở Application

- **Ngày:** 2026-11-06
- **Trạng thái:** Đã chấp nhận
- **Liên quan:** FR-RCP-003/004/005/006/007/009/010, NFR-SEC-006, decisions.md D50, D70

## Bối cảnh

`docs/permissions.md` mục 1 và FR-RCP-004 bước 5 mô tả kiểm tra quyền sở hữu bằng
`IAuthorizationService.AuthorizeAsync(user, recipe, Operations.Update)` của ASP.NET Core, đặt trong
Application Layer. Thực tế:

- Application chỉ reference Domain, FluentValidation, MediatR (CONS-001). `IAuthorizationService`
  nằm trong `Microsoft.AspNetCore.Authorization` và cần `ClaimsPrincipal`.
- `ICurrentUser` (hợp đồng chung của A, đã đóng băng) không lộ `ClaimsPrincipal`.
- Các handler ảnh của D (FR-RCP-008) và Category (D50) đã tự kiểm tra bằng `ICurrentUser`.

## Quyết định

Tạo `Application/Recipes/Authorization/RecipeAuthorizationHandler` (sealed, đăng ký Scoped), chỉ dùng
`ICurrentUser`:

- `EnsureCanCreate()`: phải có role Author hoặc Admin.
- `EnsureCanModify(recipe)`: Admin, hoặc `recipe.AuthorId == currentUser.UserId`.
- Chưa đăng nhập → `UnauthorizedException` (401); không đủ quyền → `ForbiddenException`
  `RECIPE_FORBIDDEN` (403).

Mọi command của C trên Recipe và entity con gọi handler này **trước khi ghi** dữ liệu.

## Phương án đã cân nhắc

1. **`IAuthorizationService` + `AuthorizationHandler<OperationAuthorizationRequirement, Recipe>`**: khớp
   chữ trong SRS. Nhưng Application phải thêm package ASP.NET, và phải sửa `ICurrentUser` (đã đóng băng)
   để lộ `ClaimsPrincipal`. Lợi ích không đáng so với chi phí.
2. **Kiểm tra trực tiếp trong từng handler** (như handler ảnh hiện tại): đơn giản, nhưng lặp lại ở 13
   command và dễ sót.
3. **Một service dùng chung ở Application** (chọn): một chỗ duy nhất cho luật "owner hoặc Admin",
   unit test được bằng mock `ICurrentUser`.

## Hệ quả

- Luật phân quyền Recipe nằm ở một file, có unit test.
- Handler ảnh của D (FR-RCP-008) vẫn tự kiểm tra; có thể chuyển sang dùng handler này sau, không bắt buộc.
- **Sửa tài liệu:** `permissions.md` mục 1 và FR-RCP-004 bước 5 — thay `IAuthorizationService.AuthorizeAsync`
  bằng `RecipeAuthorizationHandler.EnsureCanModify`.
