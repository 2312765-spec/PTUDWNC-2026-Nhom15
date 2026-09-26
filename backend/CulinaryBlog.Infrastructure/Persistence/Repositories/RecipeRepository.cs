using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IRecipeRepository
/// </summary>
public sealed class RecipeRepository(CulinaryBlogDbContext context) : IRecipeRepository
{
    public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => 
        Task.FromResult<Recipe?>(null);

    public Task<Recipe?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => 
        Task.FromResult<Recipe?>(null);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default) => 
        Task.FromResult(false);

    public Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default) => 
        Task.CompletedTask;

    public void Update(Recipe recipe) { }

    public void Delete(Recipe recipe) { }

    public async Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default) =>
        await context.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedByCategoryIdAsync(
        Guid categoryId, 
        RecipeStatus? status, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

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