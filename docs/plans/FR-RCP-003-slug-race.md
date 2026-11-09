# FR-RCP-003 / D85 — Race khi sinh slug lúc tạo recipe song song

> 2026-11-09. Phát hiện khi chạy E2E `a11y.spec.ts` + `responsive.spec.ts` với nhiều worker.

## Hiện tượng

`POST /api/v1/recipes` trả **500** khi hai request cùng tiêu đề đến gần như cùng lúc. Log API:

```
DbUpdateException → 23505: duplicate key value violates unique constraint "IX_Recipes_Slug"
```

## Nguyên nhân

`CreateRecipeCommandHandler.GenerateUniqueSlugAsync` kiểm tra rồi mới chèn, hai bước không nguyên tử:

1. A: `ExistsBySlugAsync("x")` → chưa có.
2. B: `ExistsBySlugAsync("x")` → chưa có (A chưa lưu).
3. A lưu OK; B vi phạm unique index → `DbUpdateException` → middleware không map → 500.

Vi phạm D10 ("slug trùng → auto-suffix, không bao giờ 409", ngầm hiểu cũng không 500).

## Hướng sửa (thử lại có giới hạn)

| Tầng | File | Thay đổi |
|---|---|---|
| Domain | `Interfaces/IRecipeRepository.cs` | Thêm `Task<bool> TryAddAsync(Recipe, CancellationToken)`: thêm + lưu; trả `false` nếu slug bị request khác chiếm. |
| Infrastructure | `Persistence/Repositories/RecipeRepository.cs` | Hiện thực: `SaveChangesAsync`, bắt `DbUpdateException` có inner `PostgresException` `23505` + `ConstraintName == "IX_Recipes_Slug"` → gỡ graph recipe (recipe, steps, ingredients, images, nutrition) khỏi change tracker → `false`. Lỗi khác ném tiếp. |
| Application | `Recipes/Commands/Create/CreateRecipeCommandHandler.cs` | Vòng tối đa 5 lần (N request cùng tiêu đề → request xui nhất thua tối đa N−1 vòng): sinh slug → dựng recipe → `TryAddAsync`. `false` → sinh lại slug (lúc này thấy slug của bên thắng → `-2`…). Hết 5 lần → ném lỗi (500, bất thường thật). |
| Docs | `docs/decisions.md` | Thêm D85. |
| Docs | `docs/traceability.md` | Ghi chú FR-RCP-003. |

**Vì sao không đổi `DbContext.SaveChangesAsync` chung:** ảnh hưởng mọi lệnh ghi (kể cả Identity,
refresh token); chỉ chỗ này cần xử lý trùng slug. Repository tự lưu đã có tiền lệ (`SaveStepsAsync`, D73).

**Vì sao không khóa (advisory lock / LOCK TABLE):** tuần tự hóa mọi lệnh tạo recipe để chống một
race hiếm; thử lại chỉ tốn thêm khi thật sự trùng.

## Test

- **Integration** (`Recipes/CreateTests.cs`): 5 `POST /recipes` song song cùng tiêu đề → cả 5 trả 201,
  5 slug khác nhau, tập slug = `{x, x-2, …, x-5}`.
- **Unit** (`CreateRecipeCommandHandlerTests.cs`): `TryAddAsync` trả `false` lần đầu → handler sinh lại
  slug và lưu được; trả `false` cả 5 lần → ném lỗi, không lặp vô hạn.

## Ngoài phạm vi

- E2E không sửa (lỗi là của backend). Lỗi timeout/đăng nhập khi chạy E2E song song là do `next dev`
  biên dịch route lần đầu chậm — chuyện riêng.
