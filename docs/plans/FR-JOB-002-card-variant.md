# FR-JOB-002 / D88 — Biến thể ảnh `card` cắt đúng khung card (NFR-PERF-005)

> 2026-11-09. Tiếp theo D86/D87: LCP `/search?q=ga` trên CI trung vị 2604ms (ngưỡng 2500ms).

## Vì sao

- Card hiển thị dải ảnh cao 160px (`object-cover`), rộng ~360–380px → tỉ lệ ~2.4:1. Ảnh gửi về là 4:3
  (750×562 qua `/_next/image`) — hơn nửa điểm ảnh bị CSS che.
- Đo trên 3 ảnh đầu của `/search?q=ga` (WebP q50, rộng 750): 4:3 = 108 KB, cắt 750×280 = 67 KB (**−38%**).
  Người dùng thấy y hệt. (AVIF chỉ −5% — loại.)
- Trước LCP `/search` tải 295 KB; trang chi tiết tải 230 KB và pass ổn định (~1.9s).

## Thiết kế

- Biến thể thứ ba `card`: **crop giữa 760×320**, WebP, không phóng to (ảnh nhỏ hơn thì cắt đúng tỉ lệ ở kích thước
  gốc). Đủ nét cho DPR 2 ở mọi kiểu lưới (1/2/3 cột).
- `RecipeSummaryDto.FeaturedImageCardUrl` (nullable). `FeaturedImageUrl` giữ là ảnh gốc.
- Frontend: `RecipeCard` dùng `featuredImageCardUrl ?? featuredImageUrl` → ảnh chưa có card vẫn hiện như cũ.

## File theo tầng

| Tầng | File | Thay đổi |
|---|---|---|
| Domain | `Entities/RecipeImage.cs`, `Entities/Recipe.cs` | `CardUrl`; `SetImageVariants(id, medium, thumbnail, card)` |
| Application | `Common/Interfaces/IImageResizer.cs` | `ResizedImageSet(Medium, Thumbnail, Card)` |
| Application | `Recipes/Commands/Images/GenerateVariants/…Handler.cs` | Bỏ qua khi **đã có `CardUrl`** (thay vì `ThumbnailUrl`). Đã có medium/thumbnail → chỉ upload card |
| Application | `Recipes/Commands/Images/Delete/…Handler.cs` | Xóa cả file card |
| Application | `Categories/DTOs/CategoryDetailResponseDto.cs` (`RecipeSummaryDto`) + `GetRecipes`, `SearchRecipes`, `GetCategoryBySlug` | `FeaturedImageCardUrl` |
| Infrastructure | `Files/SkiaImageResizer.cs` | `ToCard` |
| Infrastructure | `Persistence/Configurations/RecipeImageConfiguration.cs` + migration `AddRecipeImageCardUrl` | Cột `CardUrl varchar(500)` null |
| Infrastructure | `Persistence/Seeding/DbSeeder.cs` | Ảnh seed chưa có `CardUrl` → enqueue job (DB dev cũ cũng có card) |
| Frontend | `lib/types.ts`, `components/recipes/RecipeCard.tsx` | `featuredImageCardUrl` |
| Docs | `decisions.md` D88, `traceability.md` | |

## Test (viết trước)

- Unit `GenerateRecipeImageVariantsCommandHandlerTests`: ảnh mới → 3 biến thể; có medium/thumbnail, chưa card → chỉ
  upload card; đã có card → no-op. Unit xóa ảnh → xóa cả card.
- Integration `SkiaImageResizerTests`: card 760×320 WebP; ảnh nhỏ không phóng to, giữ tỉ lệ. `ImageResizeTests`: job
  ghi `CardUrl`. Danh sách / tìm kiếm trả `featuredImageCardUrl`.
- Frontend `RecipeCard`: ưu tiên ảnh card, không có thì ảnh gốc.

## Giới hạn

- Ảnh người dùng đã upload trên production chưa có card tới khi được xử lý lại (card dùng ảnh gốc — không hỏng).
  Backfill production ngoài phạm vi (ghi trong D88).
- Chạm file của B (DTO) và D (job resize) — báo trong PR.
- Branch `feature/FR-JOB-002-card-variant` tách từ PR #86 (`chore/dev-seed-images`).
