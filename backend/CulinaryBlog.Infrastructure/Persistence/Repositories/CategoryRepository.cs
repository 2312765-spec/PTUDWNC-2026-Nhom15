using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
{
    return await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}


public Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
{
    _context.Categories.Remove(category);
    return Task.CompletedTask;
}

public async Task<bool> ExistsByNameExcludingIdAsync(string name, Guid currentCategoryId, CancellationToken cancellationToken = default)
{
    return await _context.Categories
        .AnyAsync(c => c.Name.ToLower() == name.ToLower() && c.Id != currentCategoryId, cancellationToken);
}
public async Task<int> CountRecipesAsync(Guid categoryId, CancellationToken cancellationToken = default)
{
    return await _context.Recipes.CountAsync(r => r.CategoryId == categoryId, cancellationToken);
}
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<(Category Category, int PublishedRecipeCount)>> GetAllWithRecipeCountAsync(
        CancellationToken cancellationToken = default)
    {
        // Query category kèm đếm số lượng công thức Published (FR-CAT-001)
        var result = await _context.Categories
            .AsNoTracking()
            .Select(c => new
            {
                Category = c,
                PublishedCount = c.Recipes.Count(r => r.Status == RecipeStatus.Published)
            })
            .ToListAsync(cancellationToken);

        return result.Select(x => (x.Category, x.PublishedCount)).ToList();
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLower();
        return await _context.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == lower, cancellationToken);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug == slug, cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(category, cancellationToken);
    }

    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetRecipesByCategoryAsync(
        Guid categoryId, 
        string? currentUserId, 
        bool isAdmin, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        // Phân quyền hiển thị (FR-CAT-002 Luồng chính bước 4):
        // Guest: chỉ xem Published
        // Author: xem Published + Draft của chính mình
        // Admin: xem tất cả
        var query = _context.Recipes
            .AsNoTracking()
            .Where(r => r.CategoryId == categoryId);

        if (isAdmin)
        {
            // Admin thấy tất cả
        }
        else if (!string.IsNullOrEmpty(currentUserId))
        {
            // Author thấy Published + Draft của mình
            query = query.Where(r => r.Status == RecipeStatus.Published || (r.AuthorId == currentUserId && r.Status == RecipeStatus.Draft));
        }
        else
        {
            // Guest chỉ thấy Published
            query = query.Where(r => r.Status == RecipeStatus.Published);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Phân trang OFFSET-based: SKIP (page-1)*pageSize TAKE pageSize (SRS FR-CAT-002 bước 5)
        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
