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

    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchPublishedRecipesAsync(
        string? searchTerm, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        // 1. Chỉ lấy Recipe đã Published và chưa bị xóa mềm
        var baseQuery = context.Recipes
            .AsNoTracking()
            .Include(r => r.Images)
            .Where(r => !r.IsDeleted && r.Status == RecipeStatus.Published);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var raw = searchTerm.Trim();

            // Làm sạch từ khóa để tránh lỗi cú pháp tsquery: chỉ giữ chữ + số (kể cả chữ có dấu).
            // Danh sách đen ký tự cũ bỏ sót toán tử "<->", "<N>" của tsquery → to_tsquery ném lỗi → 500.
            var cleanTerms = System.Text.RegularExpressions.Regex.Split(raw, @"[^\p{L}\p{N}]+")
                .Where(t => t.Length > 0)
                .ToArray();

            if (cleanTerms.Length > 0)
            {
                // Xây dựng tsquery dạng prefix matching theo SRS: "pho:* & bo:*"
                var tsQueryString = string.Join(" & ", cleanTerms.Select(t => $"{t}:*"));

                // Dựng pattern ILIKE phía C#: nội suy chuỗi bên trong lambda của .All() không dịch được sang SQL
                // (EF ném "Translation of method 'string.Format' failed" → mọi request search trả 500).
                var likePatterns = cleanTerms.Select(t => $"%{t}%").ToArray();

                // Dùng EF.Functions của PostgreSQL với unaccent để tìm kiếm không dấu ("pho" ra "Phở")
                // Kết hợp cả Full-Text Search và ILike unaccent để đạt độ chính xác 100%
                baseQuery = baseQuery.Where(r =>
                    EF.Functions.ToTsVector("simple", EF.Functions.Unaccent(r.Title + " " + (r.Description ?? "")))
                        .Matches(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQueryString)))
                    || likePatterns.All(p =>
                        EF.Functions.ILike(EF.Functions.Unaccent(r.Title), EF.Functions.Unaccent(p)) ||
                        (r.Description != null && EF.Functions.ILike(EF.Functions.Unaccent(r.Description), EF.Functions.Unaccent(p)))));
            }
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // 2. Sắp xếp theo mức độ liên quan (Title khớp chính xác sẽ lên đầu) rồi đến ngày tạo
        var items = await baseQuery
            .OrderByDescending(r => r.Title.ToLower() == (searchTerm ?? "").Trim().ToLower())
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}