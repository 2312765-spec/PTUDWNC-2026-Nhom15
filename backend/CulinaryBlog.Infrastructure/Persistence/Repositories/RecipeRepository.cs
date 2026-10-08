using System.Text;
using System.Text.RegularExpressions;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public sealed class RecipeRepository : IRecipeRepository
{
    private readonly CulinaryBlogDbContext _context;

    public RecipeRepository(CulinaryBlogDbContext context)
    {
        _context = context;
    }

    public async Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

    public async Task<Recipe?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        await _context.Recipes
            .FirstOrDefaultAsync(r => r.Slug == slug && !r.IsDeleted, cancellationToken);

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        await _context.Recipes
            .AnyAsync(r => r.Slug == slug && !r.IsDeleted, cancellationToken);

    public async Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default) =>
        await _context.Recipes.AddAsync(recipe, cancellationToken);

    public void Update(Recipe recipe) =>
        _context.Recipes.Update(recipe);

    // D1/D2 — Gọi SoftDelete() theo đúng BaseEntity
    public void Delete(Recipe recipe)
    {
        recipe.SoftDelete();
        _context.Recipes.Update(recipe);
    }

    public async Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default) =>
        await _context.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    // Triển khai đúng interface yêu cầu cho Pessimistic Lock / Update
    public async Task<Recipe?> GetByIdWithImagesForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "Recipes" WHERE "Id" = {id} FOR UPDATE""", ct);

        return await GetByIdWithImagesAsync(id, ct);
    }

    public async Task<string?> GetAuthorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => r.AuthorId)
            .FirstOrDefaultAsync(ct);

    // FR-CAT-002: Lấy công thức theo danh mục
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedByCategoryIdAsync(
        Guid categoryId, 
        RecipeStatus? status, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => r.CategoryId == categoryId && !r.IsDeleted);

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

    // FR-SRCH-001: Tìm kiếm toàn văn FTS tiếng Việt (unaccent + prefix search)
    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchPublishedRecipesAsync(
        string? searchTerm, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 50) {pageSize = 50;}
        if (pageSize < 1) {pageSize = 10;}
        if (page < 1) {page = 1;}

        var raw = (searchTerm ?? string.Empty).Trim();

        // 1. Loại bỏ các toán tử tsquery: '&|!():*\\<> để tránh lỗi 500 khi query chuỗi lạ
        var cleanTerms = Regex.Split(raw, @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 0)
            .Select(RemoveDiacritics)
            .Where(t => t.Length > 0)
            .ToArray();

        // Từ khóa rỗng hoặc toàn ký tự đặc biệt -> trả về 0 kết quả (HTTP 200 OK)
        if (cleanTerms.Length == 0)
        {
            return (Array.Empty<Recipe>(), 0);
        }

        var query = _context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => !r.IsDeleted && r.Status == RecipeStatus.Published);

        var tsQuery = string.Join(" & ", cleanTerms.Select(t => $"{t}:*"));

        var matchedQuery = query.Where(r =>
            EF.Functions.ToTsVector("simple", EF.Functions.Unaccent(r.Title + " " + (r.Description ?? "")))
                .Matches(EF.Functions.ToTsQuery("simple", tsQuery)));

        var totalCount = await matchedQuery.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return (Array.Empty<Recipe>(), 0);
        }

        var items = await matchedQuery
            .OrderByDescending(r => EF.Functions.ToTsVector("simple", EF.Functions.Unaccent(r.Title + " " + (r.Description ?? "")))
                .Rank(EF.Functions.ToTsQuery("simple", tsQuery)))
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    // FR-RCP-001, FR-SRCH-002, FR-SRCH-003, FR-SRCH-004
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
        var query = _context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => !r.IsDeleted);

        if (!includeAllStatuses)
        {
            query = string.IsNullOrEmpty(nonPublishedOwnerId)
                ? query.Where(r => r.Status == RecipeStatus.Published)
                : query.Where(r => r.Status == RecipeStatus.Published || r.AuthorId == nonPublishedOwnerId);
        }

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

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}