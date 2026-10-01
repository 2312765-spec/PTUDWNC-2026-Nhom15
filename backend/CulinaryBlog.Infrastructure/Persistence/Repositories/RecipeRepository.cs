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
        recipe.SoftDelete();
        context.Recipes.Update(recipe);
    }

    public async Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default) =>
        await context.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

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

    // FR-CAT-002: Lấy công thức theo danh mục
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedByCategoryIdAsync(
        Guid categoryId, 
        RecipeStatus? status, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => r.CategoryId == categoryId && !r.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    // FR-SRCH-001: Tìm kiếm toàn văn FTS tiếng Việt
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchPublishedRecipesAsync(
        string? searchTerm, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => !r.IsDeleted && r.Status == RecipeStatus.Published);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var raw = searchTerm.Trim();
            var cleanTerms = System.Text.RegularExpressions.Regex.Split(raw, @"[^\p{L}\p{N}]+")
                .Where(t => t.Length > 0)
                .ToArray();

            if (cleanTerms.Length > 0)
            {
                var tsQueryString = string.Join(" & ", cleanTerms.Select(t => $"{t}:*"));
                var likePatterns = cleanTerms.Select(t => $"%{t}%").ToArray();

                baseQuery = baseQuery.Where(r =>
                    EF.Functions.ToTsVector("simple", EF.Functions.Unaccent(r.Title + " " + (r.Description ?? "")))
                        .Matches(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQueryString)))
                    || likePatterns.All(p =>
                        EF.Functions.ILike(EF.Functions.Unaccent(r.Title), EF.Functions.Unaccent(p)) ||
                        (r.Description != null && EF.Functions.ILike(EF.Functions.Unaccent(r.Description), EF.Functions.Unaccent(p)))));
            }
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(r => r.Title.ToLower() == (searchTerm ?? "").Trim().ToLower())
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    // FR-RCP-001, FR-SRCH-002, FR-SRCH-003, FR-SRCH-004: Danh sách công thức có lọc, sắp xếp, phân trang
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedRecipesAsync(
        Guid? categoryId,
        string? difficulty,
        int? maxCookTime,
        int? minServings,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => !r.IsDeleted && r.Status == RecipeStatus.Published);

        // FR-SRCH-002: Lọc công thức
        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(r => r.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            var diff = difficulty.Trim().ToLowerInvariant();
            if (diff is "easy" or "1"){                query = query.Where(r => r.Difficulty == RecipeDifficulty.Easy);
}
            else if (diff is "medium" or "2")
{                query = query.Where(r => r.Difficulty == RecipeDifficulty.Medium);
}            else if (diff is "hard" or "3")
{                query = query.Where(r => r.Difficulty == RecipeDifficulty.Hard);
}        }

        if (maxCookTime.HasValue)
        {
            query = query.Where(r => r.CookTime <= maxCookTime.Value);
        }

        if (minServings.HasValue)
        {
            query = query.Where(r => r.Servings >= minServings.Value);
        }

        // FR-SRCH-003: Sắp xếp kết quả
        var s = (sort ?? "-createdat").Trim().ToLowerInvariant();
        query = s switch
        {
            "createdat" => query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
            "-createdat" => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id),
            "title" => query.OrderBy(r => r.Title).ThenBy(r => r.Id),
            "-title" => query.OrderByDescending(r => r.Title).ThenBy(r => r.Id),
            "cooktime" => query.OrderBy(r => r.CookTime).ThenBy(r => r.Id),
            "-cooktime" => query.OrderByDescending(r => r.CookTime).ThenBy(r => r.Id),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        // FR-SRCH-004: Phân trang
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}