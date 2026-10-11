using System.Text.RegularExpressions;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

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

    /// <summary>
    /// FR-RCP-002 — chi tiết theo slug kèm category, steps, ingredients, images (NFR-PERF-004: không
    /// N+1). AsSplitQuery: ba collection trong một JOIN sẽ nhân số dòng (cartesian explosion).
    /// Global Query Filter đã loại các con đã soft-delete.
    /// </summary>
    public async Task<Recipe?> GetBySlugDetailedAsync(string slug, CancellationToken cancellationToken = default) =>
        await context.Recipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Category)
            .Include(r => r.Steps)
            .Include(r => r.Ingredients)
            .Include(r => r.Images)
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

    /// <summary>
    /// FR-SRCH-001/D49 — lọc bằng <c>SearchVector @@ to_tsquery</c> (dùng GIN index
    /// IX_Recipes_SearchVector), xếp theo <c>ts_rank</c>. Từ khóa tách theo chữ/số (loại toán tử
    /// tsquery như <c>&amp; | ! : * ( ) &lt;-&gt;</c> → không bao giờ lỗi cú pháp), mỗi từ thành
    /// tiền tố <c>từ:*</c> nối bằng <c>&amp;</c>, rồi unaccent ở phía PostgreSQL — cùng hàm với trigger.
    /// </summary>
    public async Task<(IReadOnlyList<(Recipe Recipe, float Rank)> Items, int TotalCount)> SearchPublishedRecipesAsync(
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var terms = Regex.Split(searchTerm ?? string.Empty, @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 0)
            .ToArray();
        if (terms.Length == 0)
        {
            return ([], 0);
        }

        var tsQuery = string.Join(" & ", terms.Select(t => $"{t}:*"));

        var matches = context.Recipes
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Status == RecipeStatus.Published)
            .Where(r => EF.Property<NpgsqlTsVector>(r, RecipeConfiguration.SearchVector)
                .Matches(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQuery))));

        var totalCount = await matches.CountAsync(cancellationToken);

        var ranked = await matches
            .Select(r => new
            {
                r.Id,
                Rank = EF.Property<NpgsqlTsVector>(r, RecipeConfiguration.SearchVector)
                    .Rank(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQuery))),
                r.CreatedAt,
            })
            .OrderByDescending(x => x.Rank)
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Nạp entity kèm ảnh cho đúng một trang (một query, không N+1), giữ thứ tự theo rank.
        var ids = ranked.Select(x => x.Id).ToList();
        var recipes = await context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return (ranked.Select(x => (recipes[x.Id], x.Rank)).ToList(), totalCount);
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