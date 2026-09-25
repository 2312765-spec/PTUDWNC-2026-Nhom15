using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>
/// FR-RCP-008 (D) chỉ cần method này. FR-RCP-001..007 (C, S7) sẽ bổ sung thêm method vào
/// IRecipeRepository (GetBySlugAsync, danh sách có filter...) khi tới lượt.
/// </summary>
public sealed class RecipeRepository(CulinaryBlogDbContext context) : IRecipeRepository
{
    public async Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default) =>
        await context.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<string?> GetAuthorIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Recipes
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => r.AuthorId)
            .FirstOrDefaultAsync(ct);

    /// <remarks>
    /// Hai câu lệnh, KHÔNG gộp: ở READ COMMITTED một câu <c>SELECT … FOR UPDATE</c> có JOIN ảnh dùng
    /// snapshot lấy TRƯỚC khi chờ khoá, nên request xếp sau vẫn không thấy ảnh mà request trước vừa
    /// commit. Khoá xong mới nạp bằng câu lệnh mới (snapshot mới) thì thấy đúng.
    /// CONS-006: raw SQL có tham số hoá (interpolated → DbParameter); EF không có LINQ cho FOR UPDATE.
    /// </remarks>
    public async Task<Recipe?> GetByIdWithImagesForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "Recipes" WHERE "Id" = {id} FOR UPDATE""", ct);

        return await GetByIdWithImagesAsync(id, ct);
    }
}
