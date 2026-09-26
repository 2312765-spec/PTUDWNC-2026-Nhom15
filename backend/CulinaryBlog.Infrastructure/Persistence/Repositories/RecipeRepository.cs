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
    public async Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Recipes
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

    public async Task<Recipe?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        await context.Recipes
            .FirstOrDefaultAsync(r => r.Slug == slug && !r.IsDeleted, cancellationToken);

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        await context.Recipes
            .AnyAsync(r => r.Slug == slug && !r.IsDeleted, cancellationToken);

    public async Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default) =>
        await context.Recipes.AddAsync(recipe, cancellationToken);

    public void Update(Recipe recipe) =>
        context.Recipes.Update(recipe);

    public void Delete(Recipe recipe)
    {
        recipe.IsDeleted = true;
        recipe.UpdatedAt = DateTime.UtcNow;
        context.Recipes.Update(recipe);
    }

    public async Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default) =>
        await context.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedByCategoryIdAsync(
        Guid categoryId, 
        RecipeStatus? status, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = context.Recipes
            .AsNoTracking()
            .Include(r => r.Images) // Nạp kèm Images để DTO có ảnh thumbnail
            .Where(r => r.CategoryId == categoryId && !r.IsDeleted);

        // Mặc định nếu không truyền status thì lấy Published
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }
        else
        {
            query = query.Where(r => r.Status == RecipeStatus.Published);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<string?> GetAuthorIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Recipes
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => r.AuthorId)
            .FirstOrDefaultAsync(ct);

    public async Task<Recipe?> GetByIdWithImagesForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "Recipes" WHERE "Id" = {id} FOR UPDATE""", ct);

        return await GetByIdWithImagesAsync(id, ct);
    }
}