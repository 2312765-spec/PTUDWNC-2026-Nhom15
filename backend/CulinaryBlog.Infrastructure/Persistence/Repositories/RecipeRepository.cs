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

    // FR-CAT-002: Lấy công thức theo danh mục (hỗ trợ cả Published và Draft theo quyền)
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

        // Mặc định nếu không truyền status thì lấy Published — tránh lộ Draft khi người gọi quên truyền.
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

    // FR-RCP-001, FR-SRCH-002 (Lọc), FR-SRCH-003 (Sắp xếp), FR-SRCH-004 (Phân trang)
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedRecipesAsync(
        Guid? categoryId,
        RecipeDifficulty? difficulty,
        int? maxCookTime,
        int? minServings,
        string? sort,
        bool includeAllStatuses,
        string? nonPublishedOwnerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => !r.IsDeleted);

        // FR-RCP-001: Guest → Published · Author → + Draft/Archived của mình · Admin → tất cả
        if (!includeAllStatuses)
        {
            query = string.IsNullOrEmpty(nonPublishedOwnerId)
                ? query.Where(r => r.Status == RecipeStatus.Published)
                : query.Where(r => r.Status == RecipeStatus.Published || r.AuthorId == nonPublishedOwnerId);
        }

        // FR-SRCH-002: Lọc công thức (AND logic)
        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(r => r.CategoryId == categoryId.Value);
        }

        if (difficulty.HasValue)
        {
            query = query.Where(r => r.Difficulty == difficulty.Value);
        }

        if (maxCookTime.HasValue)
        {
            query = query.Where(r => r.CookTime <= maxCookTime.Value);
        }

        if (minServings.HasValue)
        {
            query = query.Where(r => r.Servings >= minServings.Value);
        }

        // FR-SRCH-003: Sắp xếp kết quả (mặc định -createdAt)
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

        // FR-SRCH-004: Phân trang SKIP / TAKE
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}