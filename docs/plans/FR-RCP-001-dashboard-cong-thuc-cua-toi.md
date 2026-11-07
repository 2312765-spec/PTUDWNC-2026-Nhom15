# Kế hoạch FR-RCP-001 (mở rộng) — `/dashboard/recipes`: công thức của tôi

> Endpoint: `GET /api/v1/recipes?mine=true&status=` · Route FE: `/dashboard/recipes` (SRS 5.1, CSR) · Tác nhân: Author/Admin
> Liên quan: FR-RCP-001, FR-RCP-003→007 (trang là lối vào quản lý), D4, D8, D14, D74, D77–D80, **D81 (mới)**.
> Nhánh: `feature/FR-RCP-001-dashboard-my-recipes`.

## Hiện trạng

- SRS 5.1 có route `/dashboard/recipes` "Quản lý danh sách recipe của user" nhưng bảng 8.3 không có endpoint
  "recipe của tôi". `GET /recipes` trả Author tập "Published của mọi người + Draft/Archived của mình" — trộn bài người khác.
- `GetRecipesQuery` đã có filter/sort/paging/validator + cache key tách theo người xem. **File của B** (`Recipes/Queries/OWNER.md`)
  → PR ghi chú để B review.
- `RecipeSummaryDto` có `Status` (chuỗi) + `CreatedAt`, không có `UpdatedAt`/tên danh mục — giữ nguyên.
- FE có `RecipeCard`, `Pagination`, `Badge`, `Skeleton`; `app/dashboard/recipes/layout.tsx` đã chặn quyền Author/Admin.

## D81 (ghi vào `decisions.md` trước khi code)

1. `GET /api/v1/recipes?mine=true` → chỉ recipe có `AuthorId == currentUserId`, mọi trạng thái (trừ đã xóa mềm).
   Không tạo route `/recipes/mine` (đụng `/recipes/{slug}` — recipe tên "Mine" có slug `mine`).
2. `mine=true` khi chưa đăng nhập → **401** `AUTH_TOKEN_INVALID`.
3. Bộ lọc `status` (`Draft|Published|Archived`, không phân biệt hoa thường) áp **sau** bộ lọc quyền — Guest gửi
   `status=Draft` chỉ nhận rỗng. Giá trị lạ → 400 `VALIDATION_ERROR`.
4. Cache key thêm `mine` + `status`; tag `recipes` vẫn bị xóa sau mọi lệnh ghi recipe (D8) — không cần cơ chế mới.
5. Admin vào `/dashboard/recipes` cũng chỉ thấy bài của mình (SRS "của user"); màn quản lý toàn bộ recipe ngoài scope v1.
6. DTO giữ nguyên.

## File tạo / sửa theo tầng

### 0. Tài liệu
- `docs/decisions.md` — D81 (bảng tra nhanh + mục chi tiết + "Sửa SRS": FR-RCP-001, bảng 8.3).

### 1. Application (file của B)
- `Recipes/Queries/GetRecipes/GetRecipesQuery.cs` — `bool Mine = false`, `string? Status = null`, đưa vào `CacheKey`.
- `.../GetRecipesQueryValidator.cs` — `Status` ∈ `draft|published|archived`.
- `.../GetRecipesQueryHandler.cs` — `Mine` + chưa đăng nhập → `UnauthorizedException(AUTH_TOKEN_INVALID)`; truyền
  `authorId`, `status` xuống repository.

### 2. Domain / Infrastructure
- `Domain/Interfaces/IRecipeRepository.cs` — `GetPagedRecipesAsync` thêm `string? authorId`, `RecipeStatus? status`.
- `Infrastructure/Persistence/Repositories/RecipeRepository.cs` — sau bộ lọc quyền: `AuthorId == authorId`, `Status == status`.
  Cập nhật mọi chỗ gọi.

### 3. API
- `API/Endpoints/RecipeEndpoints.cs` — `MapGet("/")` nhận `bool? mine`, `string? status`; mô tả Scalar; khai báo 401.

### 4. Test (viết trước, xác nhận fail)
- `IntegrationTests/Recipes/GetRecipesTests.cs` — `mine=true` chỉ bài của mình (đủ 3 trạng thái, không bài người khác,
  không bài xóa mềm); chưa đăng nhập 401; `mine` + `status=Draft`; Admin `mine` chỉ bài của Admin; Guest `status=Draft`
  → rỗng; `status=abc` → 400; archive xong `mine&status=Archived` thấy ngay (cache).
- `UnitTests/Recipes/GetRecipesTests.cs` — validator `status`; handler `mine` chưa đăng nhập → 401; cache key khác nhau.

### 5. Frontend
- `frontend/lib/recipe-editor-api.ts` — `fetchMyRecipes({ status?, page })` (apiClient có Bearer — D74) + `recipeEditorKeys.mine(...)`.
- `frontend/app/dashboard/recipes/page.tsx` — CSR; tablist Tất cả/Nháp/Đã xuất bản/Đã lưu trữ (`?status=` trên URL);
  dòng: tiêu đề, badge, ngày tạo, link Sửa, link Xem (chỉ Published); `Pagination`; skeleton; trạng thái rỗng + CTA
  "Tạo công thức mới"; lỗi + "Thử lại".
- `frontend/app/dashboard/page.tsx` — lối tắt "Công thức của tôi".
- `components/recipes/editor/RecipePublishControls.tsx` — invalidate query `mine` sau publish/archive/delete; xóa xong về
  `/dashboard/recipes`.
- `__tests__/app/dashboard/recipes/page.test.tsx` — render, đổi tab gọi API đúng `status`, rỗng, lỗi + thử lại, link Xem.
- `e2e/recipe-editor.spec.ts` — sau wizard: `/dashboard/recipes` thấy công thức ở tab Nháp.

### 6. Hoàn tất
- `docs/traceability.md` (FR-RCP-001 + D81), `frontend/app/dashboard/OWNER.md`.
- `dotnet build` 0 warning · `dotnet test` · lint · Jest · `npm run build` · E2E.
- Commit `feat(FR-RCP-001): danh sách công thức của tôi — ?mine=true&status= + /dashboard/recipes (D81)`.
