# Kế hoạch — NFR-PERF-004: query danh mục, index CreatedAt, cảnh báo slow query

**Ngày:** 2026-11-10 · **Branch:** `feature/NFR-PERF-004-categories-query`
**Phạm vi:** backend. Không có FR mới — sửa cách hiện thực FR-CAT-001, FR-CAT-002, FR-RCP-001 cho đúng
NFR-PERF-004. Quyết định áp dụng: D2, D8, D14. Có **migration** (đã báo nhóm 2026-11-10).

## 1. Hiện trạng

SRS NFR-PERF-004: không N+1 — bắt buộc `.Include()` **và projection**; mọi cột WHERE/ORDER BY có B-tree index;
cảnh báo khi query > 100 ms; EXPLAIN ANALYZE pass review.

Số đo `backend/tests/performance/results/perf-summary.md` (2026-11-03, 10.050 recipes):

| # | Chỗ | Vấn đề |
|---|---|---|
| 1 | `GET /categories` — `CategoryRepository.GetAllWithRecipesAsync` | Nạp 8.017 recipe Published **kèm ảnh** chỉ để đếm `recipeCount` trong bộ nhớ. **532,6 ms**, sort tràn đĩa. Cache trống + 5 request đồng thời → p95 13,8 s |
| 2 | `GET /recipes` `ORDER BY "CreatedAt" DESC, "Id"` | Không có index → Seq Scan + top-N heapsort (7 ms, tăng tuyến tính) |
| 3 | Cảnh báo slow query > 100 ms | Chưa có (chỉ có `LoggingBehavior` > 500 ms cho cả request MediatR) |
| 4 | **Phát hiện thêm** — `GET /categories/{slug}` — `CategoryRepository.GetBySlugAsync` | Nạp **toàn bộ** recipe + ảnh của danh mục rồi lọc quyền, đếm, phân trang **trong bộ nhớ**. Query không có cache (không `ICacheable`) |

`RecipeRepository.GetPagedByCategoryIdAsync` đã phân trang trong SQL nhưng chưa ai gọi, và điều kiện lọc khác handler
(một `status` duy nhất, không có "Draft của chính mình").

## 2. Test trước

Đếm/đọc SQL thật bằng log `Microsoft.EntityFrameworkCore.Database.Command` (bắt qua `ILogEventSink`, cùng cách
`StructuredLoggingTests`), Postgres Testcontainers.

| # | File | Kịch bản | Kỳ vọng |
|---|---|---|---|
| 1 | `IntegrationTests/Categories/CategoryQueryShapeTests.cs` | `GET /categories` | Đúng **1** câu SQL, không chứa `RecipeImages`, có `count(`; `recipeCount` chỉ đếm Published chưa xóa |
| 2 | như trên | `GET /categories/{slug}?page=2&pageSize=2` | SQL lấy recipe có `LIMIT`; không nạp recipe ngoài trang; tổng/trang đúng; Guest không thấy Draft, chủ sở hữu thấy Draft của mình, không thấy Draft người khác, không ai thấy Archived |
| 3 | `IntegrationTests/Common/SchemaDriftTests.cs` hoặc file mới | Index | Có `IX_Recipes_Status_CreatedAt` trên `("Status", "CreatedAt" DESC, "Id")` |
| 4 | `IntegrationTests/Observability/SlowQueryTests.cs` | Câu SQL chạy > 100 ms (`pg_sleep(0.15)`) | 1 log **Warning** có thời gian + câu SQL; câu nhanh không có Warning |

Test hiện có của FR-CAT-001/002 (`GetCategoriesTests`, `GetCategoryBySlugTests`, unit test handler) phải vẫn xanh.

## 3. Code

| Tầng | File | Thay đổi |
|---|---|---|
| Domain | `Interfaces/ICategoryRepository.cs` | Thêm `GetAllWithPublishedRecipeCountAsync()` → `IReadOnlyList<CategorySummary>` (record BCL: `Id, Name, Slug, Description, PublishedRecipeCount`). **Bỏ** `GetAllWithRecipesAsync`. `GetBySlugAsync` không Include recipe nữa |
| Domain | `Interfaces/IRecipeRepository.cs` | `GetPagedByCategoryIdAsync(categoryId, draftOwnerId, page, pageSize)` — Published, cộng Draft khi `AuthorId == draftOwnerId` |
| Application | `GetCategoriesQueryHandler` | Map từ `CategorySummary` |
| Application | `GetCategoryBySlugQueryHandler` | `GetBySlugAsync` (chỉ danh mục) + `GetPagedByCategoryIdAsync`; giữ nguyên DTO, quy tắc quyền và clamp `pageSize` |
| Infrastructure | `Repositories/CategoryRepository.cs` | Projection `c.Recipes.Count(r => Published && !IsDeleted)` trong SQL |
| Infrastructure | `Persistence/Repositories/RecipeRepository.cs` | Sửa `GetPagedByCategoryIdAsync`; `ORDER BY CreatedAt DESC, Id` |
| Infrastructure | `Persistence/Configurations/RecipeConfiguration.cs` + migration `AddRecipeStatusCreatedAtIndex` | Index `("Status", "CreatedAt" DESC, "Id")` |
| Infrastructure | `Persistence/Interceptors/SlowQueryInterceptor.cs` (mới) + `DependencyInjection.cs` | `DbCommandInterceptor`: `Duration > 100 ms` → `LogWarning` (thời gian, câu SQL — không log giá trị tham số) |
| Unit | `UnitTests/Categories/*` | Cập nhật mock theo interface mới |

## 4. Đo lại và tài liệu

- `backend/tests/performance/explain-analyze.sql`: thay SQL mục 1, 2b bằng SQL mới, thêm câu `/categories/{slug}`;
  chạy trên `seed-perf-data.sql`, ghi số mới vào `perf-summary.md`.
- `docs/traceability.md`: NFR-PERF-004 → theo kết quả; ghi chú ở FR-CAT-001/002.

## 5. Kết quả (2026-11-10)

| Test | Trước khi sửa | Sau khi sửa |
|---|---|---|
| `/categories` 1 câu SQL, không đọc `RecipeImages`, có `count(` | ❌ | ✅ |
| `/categories/{slug}` phân trang trong SQL | ❌ (lần đầu test sai — đã pass vì `LIMIT 1` của Categories; đổi điều kiện sang `OFFSET`) | ✅ |
| Quyền xem FR-CAT-002 giữ nguyên | ✅ | ✅ |
| Index `IX_Recipes_Status_CreatedAt` | ❌ | ✅ |
| Slow query > 100 ms → Warning / nhanh → không | ❌ / ✅ | ✅ / ✅ |

Số đo trước/sau: `backend/tests/performance/results/perf-summary.md` mục "Đo lại sau khi sửa — 2026-11-10".

Lệch so với kế hoạch: `SlowQueryInterceptor` đặt ở `Infrastructure/Persistence/` (cùng chỗ `AuditInterceptor`,
`RowVersionInterceptor`), không tạo thư mục `Interceptors/`. `CategorySummary` là record trong
`Domain/Interfaces/ICategoryRepository.cs`.

## 6. Ngoài phạm vi

- Cache cho `/categories/{slug}` (D8 không có key cho query này — cần mục D riêng).
- NFR-PERF-001 đo write operations, NFR-PERF-002 giới hạn tài nguyên DB/Redis.
