# Kế hoạch FR-RCP-006 — Lưu trữ công thức (Archive)

> Endpoint: `PATCH /api/v1/recipes/{id:guid}/archive` · Ưu tiên: S (Should Have) · Tác nhân: Author (chủ sở hữu) / Admin
> Liên quan: FR-RCP-005 (publish/unpublish — đường thoát khỏi Archived), FR-RCP-007, D1, D4, D8, D70, D71, D77, D79 (mới).
> Nhánh: `feature/FR-RCP-006-archive-recipe`.

## Hiện trạng

- Domain đã có `Recipe.Archive()` (`Recipe.cs:156`) nhưng trả `void`, chưa idempotent — lệch với `Publish()`/`Unpublish()` (D77).
- Danh sách công khai, tìm kiếm, sitemap chỉ lấy Published → tự loại Archived. Trang chi tiết `noindex` Draft/Archived (D61).
- **Unarchive** đã có qua D77: publish/unpublish trên recipe Archived. Không cần endpoint riêng.
- Thiếu: Command/Handler/Validator, endpoint, test, nút "Lưu trữ" ở frontend.

## Quyết định áp dụng

| Quyết định | Nội dung áp dụng cho FR này |
|---|---|
| D1 | Archive không xóa gì — không đụng step/ingredient/ảnh/MinIO. |
| D4 | Lỗi theo RFC 7807, không 422. |
| D8 | Xóa tag `recipes` + `recipe:{slug}`. |
| D70 | Kiểm quyền bằng `RecipeAuthorizationHandler.EnsureCanModify`. |
| D71 | `rowVersion` + header `ETag`. |
| D77 | Unarchive = publish/unpublish. Mẫu idempotent, `If-Match` tùy chọn, khóa dòng. |

### D79 (ghi vào `decisions.md` trước khi code)

1. Archive được từ **Draft và Published**.
2. **Idempotent:** đã Archived → 200, không `SaveChanges` (RowVersion, `UpdatedAt` giữ nguyên).
3. **`If-Match` tùy chọn:** sai định dạng → 400 `VALIDATION_ERROR` (trường `RowVersion`); lệch → 409
   `RECIPE_CONCURRENCY_CONFLICT`. Thứ tự 404 → 403 → 409.
4. Response **`RecipeDetailDto`** + header **`ETag`**.
5. **Khóa dòng** recipe trong transaction; log audit "Recipe {RecipeId} chuyển sang Archived bởi {UserId}" (NFR-SEC-006).
6. **`PublishedAt` giữ nguyên** khi archive (D77.3).
7. **Không thêm metric** (D39 chỉ đếm publish).

## File tạo / sửa theo tầng

### 0. Tài liệu
- `docs/decisions.md` — dòng D79 ở bảng tra nhanh + mục chi tiết + "Sửa SRS".

### 1. Domain
- `Domain/Entities/Recipe.cs` — `Archive()` → `bool`; đã Archived → `false`, không đổi gì.
  Cập nhật chỗ gọi: `DbSeeder.cs`, `IntegrationTests/Common/ConcurrencyTests.cs`, `PublishTests.cs`, `PublishRecipeCommandTests.cs`.

### 2. Application — `Recipes/Commands/Archive/`
- `ArchiveRecipeCommand.cs` — `record ArchiveRecipeCommand(Guid RecipeId, string? IfMatch = null) : IRequest<RecipeDetailDto>, ICacheInvalidator`.
- `ArchiveRecipeCommandValidator.cs` — `If-Match` có thì đúng định dạng.
- `ArchiveRecipeCommandHandler.cs` — khóa dòng → 404 → 403 → 409 → `Archive()` → `SaveChanges` khi đổi → log → tag cache → DTO.

Command riêng (không gộp vào `PublishRecipeCommand`) — CONS-002: mỗi use case 1 command.

### 3. API
- `API/Endpoints/RecipeEndpoints.cs` — `MapPatch("/{id:guid}/archive")`, đọc `If-Match`, trả 200 + `ETag`,
  `AuthorPolicy`, khai báo 400/401/403/404/409 cho Scalar.

### 4. Test (viết trước, xác nhận fail)
- `UnitTests/Domain/RecipeTests.cs` — Draft → Archived; Published → Archived giữ `PublishedAt`; đã Archived → `false`.
- `UnitTests/Recipes/ArchiveRecipeCommandTests.cs` — 404, 403, 409, idempotent không `SaveChanges`, tag cache; validator `If-Match`.
- `IntegrationTests/Recipes/ArchiveTests.cs` — owner 200 + `ETag` và biến khỏi `GET /recipes` (Guest); Admin 200;
  Author khác 403; id lạ 404; không đăng nhập 401; `If-Match` lệch 409 / sai định dạng 400; gọi lần 2 → 200 RowVersion
  không đổi; Archived → publish → Published, `PublishedAt` giữ nguyên.

### 5. Frontend
- `frontend/lib/recipe-editor-api.ts` — `archiveRecipe(id)`.
- `components/recipes/editor/RecipePublishControls.tsx` — nút "Lưu trữ" (icon `Archive`) khi chưa Archived, confirm
  dialog, toast, `busy = 'archive'`.
- `__tests__/components/recipes/RecipePublishControls.test.tsx` — hiện/ẩn nút, hủy không gọi API, xác nhận gọi API + toast, lỗi → toast lỗi.
- *(tùy chọn)* `frontend/e2e/recipe-editor.spec.ts` — archive → trang công khai không còn.

### 6. Hoàn tất
- `docs/traceability.md` — FR-RCP-006 ✅, C 7/7, tổng 33/34.
- `dotnet build` 0 warning · `dotnet test` · `npm run lint && npm run build` · Jest.
- Commit `feat(FR-RCP-006): lưu trữ công thức — PATCH /api/v1/recipes/{id}/archive`.
