# Thư mục Entities — người phụ trách: **B**

Sprint 0 (tuần 1), B tạo **toàn bộ** entity theo SRS Chương 7, kể cả bảng của slice sau.
Một migration lớn sạch hơn năm migration vá, và nó gỡ block cho C và D ngay từ tuần 2.

| File | SRS | Ghi chú bắt buộc |
|---|---|---|
| `Recipe.cs` | 7.2 | `Instructions` **NULL** (D18) · `CookTime` cho phép 0 (D19) · owned `RecipeNutrition` |
| `RecipeStep.cs` | 7.3 | `Title` bắt buộc · `TimerMinutes` (**không** `DurationMinutes`) — D6 |
| `RecipeIngredient.cs` | 7.4 | `Quantity`/`Unit` **nullable** · `OrderIndex` (**không** `SortOrder`) — D7 |
| `RecipeImage.cs` | 7.5 | `IsPrimary`, `OrderIndex` |
| `Category.cs` | 7.6 | `Name` UNIQUE, `Slug` UNIQUE |
| ~~`ApplicationUser.cs`~~ | 7.7 | ❌ **không đặt ở Domain** — xem D23/ADR-0003: kế thừa `IdentityUser` nên vi phạm CONS-001 nếu để ở Domain. Đã tạo ở `Infrastructure/Identity/ApplicationUser.cs`, truy cập qua `IIdentityService`. |
| ✅ `RefreshToken.cs` | 7.8 | Đã tạo (A, FR-AUTH-001, prerequisite) — `TokenHash` SHA-256 · `IsRevoked` là **computed** `RevokedAt != null` (D20) |

**Quy tắc:** entity không có public setter. Tạo qua factory `Recipe.Create(...)`,
đổi trạng thái qua method `recipe.Publish()`. C sẽ viết các method đó ở Sprint 1.

Xóa file này khi đã tạo xong entity.
