# ADR-0003: ApplicationUser đặt ở tầng Infrastructure, truy cập qua IIdentityService

- **Ngày:** 2026-09-21
- **Trạng thái:** Đã chấp nhận
- **Liên quan:** FR-AUTH-001 → 007, CONS-001, CONS-002, decisions.md D20, D23

## Bối cảnh

SRS mục 1008 (bảng kiến trúc tổng quan) liệt `ApplicationUser` vào danh sách Domain
Entities. `docs/roadmap.md` và `backend/CulinaryBlog.Domain/Entities/OWNER.md` cũng ghi
`ApplicationUser.cs` là file cần tạo trong `CulinaryBlog.Domain/Entities/`, kế thừa
`IdentityUser<string>` của ASP.NET Core Identity.

Đồng thời, CONS-001 (bất biến — "vi phạm = reject PR") quy định: *"Domain KHÔNG phụ thuộc
thư viện ngoài nào (chỉ .NET BCL)"*. `CulinaryBlog.Domain.csproj` có comment tường minh:
"KHÔNG thêm `<PackageReference>` vào file này. ArchitectureTests sẽ fail nếu vi phạm."

`IdentityUser<TKey>` nằm trong gói NuGet `Microsoft.Extensions.Identity.Stores`. Để dùng
`UserManager<ApplicationUser>` với `AddEntityFrameworkStores<TContext>()` (bắt buộc cho
FR-AUTH-001 → 007: đăng ký, đăng nhập, lockout, refresh token), lớp `UserStore<TUser, TRole,
TContext, TKey>` mà thư viện dùng bên dưới yêu cầu ràng buộc generic `TUser : IdentityUser<TKey>`.
Không có cách nào dùng EF Identity Store chuẩn mà không cho `ApplicationUser` kế thừa
`IdentityUser<TKey>`.

→ Hai yêu cầu (ApplicationUser ở Domain) và (Domain zero-dependency) mâu thuẫn trực tiếp,
không thể cùng thỏa mãn.

## Quyết định

Đặt `ApplicationUser` ở `CulinaryBlog.Infrastructure/Identity/ApplicationUser.cs`, kế thừa
`IdentityUser` (khóa `string`). Domain **không có** entity User.

Application tầng trên định nghĩa interface `IIdentityService`
(`Application/Common/Interfaces/IIdentityService.cs`) làm ranh giới — không expose kiểu
`ApplicationUser` ra ngoài Infrastructure. Handler ở Application (`RegisterCommandHandler`,
sau này `LoginCommandHandler`…) chỉ biết `IIdentityService`, nhận về DTO thuần
(`CreatedUser`, `UserProfileDto`…), không bao giờ thấy `ApplicationUser` hay
`UserManager<T>`.

`Infrastructure/Identity/IdentityService.cs` hiện thực `IIdentityService` bằng
`UserManager<ApplicationUser>` + `RoleManager<IdentityRole>`.

`RefreshToken` (SRS 7.8) vẫn ở Domain — nó không kế thừa `IdentityUser`, chỉ là POCO giữ
`UserId` kiểu `string` (không navigation property đến `ApplicationUser`, vì Domain không
biết kiểu đó tồn tại). Quan hệ FK cấu hình ở Infrastructure qua Fluent API
(`HasOne().WithMany().HasForeignKey("UserId")` không cần navigation phía User).

## Phương án đã cân nhắc

1. **Đặt `ApplicationUser` ở Domain, thêm `Microsoft.Extensions.Identity.Stores` vào
   `Domain.csproj`.** — Vi phạm thẳng CONS-001 (ràng buộc bất biến, ArchitectureTests sẽ đỏ
   nếu có test kiểm tra Domain zero-package; và ngay cả khi test hiện tại chưa bắt được, đây
   vẫn là vi phạm tinh thần rule rõ ràng nhất trong 10 CONS). Bị loại.

2. **Đặt `ApplicationUser` ở Domain nhưng KHÔNG kế thừa `IdentityUser`** (tự viết lại toàn
   bộ cột Identity cần — PasswordHash, SecurityStamp, LockoutEnd…) rồi tự viết `IUserStore`,
   `IUserPasswordStore`… thủ công ở Infrastructure để map sang entity Domain. — Kỹ thuật khả
   thi nhưng tốn công lớn để tái tạo lại đúng hành vi bảo mật mà ASP.NET Core Identity đã
   kiểm thử kỹ (lockout counting, security stamp invalidation, password hasher versioning).
   Rủi ro bug bảo mật tự chế cao hơn lợi ích "giữ đúng vị trí thư mục theo tài liệu". Bị loại.

3. **Đặt `ApplicationUser` ở Infrastructure, dùng `IIdentityService` làm ranh giới (đã chọn).**
   — Đúng CONS-001 tuyệt đối, dùng nguyên `UserManager`/`AddEntityFrameworkStores` chuẩn của
   Identity (đã kiểm thử, bảo trì bởi Microsoft), và đúng luôn CONS-002 (Application chỉ biết
   contract, không biết chi tiết hiện thực) — nhất quán với mẫu `IJwtService`, `IEmailService`,
   `ICurrentUser` đã có sẵn trong repo. Cái giá: lệch một dòng so với SRS 1008 (bảng liệt kê
   tổng quan, không phải đặc tả chi tiết) và roadmap.md (tài liệu điều phối, không phải nguồn
   sự thật kiến trúc theo CLAUDE.md mục 1).

## Hệ quả

- `CulinaryBlogDbContext` kế thừa `IdentityDbContext<ApplicationUser, IdentityRole, string>`
  thay vì `DbContext` thuần.
- Domain có thêm `Interfaces/IRefreshTokenRepository.cs` (không dùng `IRepository<T>` chung
  vì `RefreshToken` không kế thừa `BaseEntity` — xem D20: không có `IsDeleted`/`RowVersion`).
- Mọi FR-AUTH sau này (002, 003, 004, 005, 006, 007) tiếp tục mở rộng `IIdentityService` thay
  vì tự ý thêm dependency Identity vào Application/Domain — giữ ranh giới này là bất biến đi
  kèm CONS-001, không cần bàn lại mỗi lần thêm command mới.
- `docs/SRS.md` mục 1008 cần sửa (xem D23) — việc này để trong hàng đợi CR, không tự sửa file
  SRS.
