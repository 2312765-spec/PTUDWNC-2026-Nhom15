using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly CulinaryBlogDbContext _dbContext;

    public CategoryRepository(CulinaryBlogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Thêm danh mục mới (FR-CAT-003)
    /// </summary>
    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _dbContext.Categories.AddAsync(category, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-004: Lấy danh mục theo ID để cập nhật
    /// </summary>
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-002: Lấy chi tiết danh mục theo Slug kèm công thức và ảnh
    /// </summary>
    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .Include(c => c.Recipes.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Images)
            .FirstOrDefaultAsync(c => c.Slug == slug && !c.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Kiểm tra tồn tại slug
    /// </summary>
    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AnyAsync(c => c.Slug == slug && !c.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Kiểm tra tồn tại danh mục theo tên (Case-insensitive)
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower() && !c.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Kiểm tra tồn tại danh mục khác có cùng tên (khi cập nhật)
    /// </summary>
    public async Task<bool> ExistsByNameExcludingIdAsync(string name, Guid excludeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower() && c.Id != excludeId && !c.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Đếm số lượng công thức Published thuộc danh mục
    /// </summary>
    public async Task<int> GetPublishedRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recipes
            .CountAsync(r => r.CategoryId == categoryId && r.Status == Domain.Enums.RecipeStatus.Published && !r.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-001: Lấy tất cả danh mục sắp xếp theo Tên tăng dần
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lấy tất cả danh mục kèm theo Recipes đã xuất bản
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .Where(c => !c.IsDeleted)
            .Include(c => c.Recipes.Where(r => !r.IsDeleted && r.Status == Domain.Enums.RecipeStatus.Published))
                .ThenInclude(r => r.Images)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public void Update(Category category)
    {
        category.UpdatedAt = DateTime.UtcNow;
        _dbContext.Categories.Update(category);
    }

    public void Delete(Category category)
    {
        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        _dbContext.Categories.Update(category);
    }
}