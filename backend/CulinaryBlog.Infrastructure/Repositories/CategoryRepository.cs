using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

/// <summary>
/// Triển khai ICategoryRepository cho FR-CAT-001 đến FR-CAT-004
/// </summary>
public class CategoryRepository : ICategoryRepository
{
    private readonly CulinaryBlogDbContext _dbContext;

    public CategoryRepository(CulinaryBlogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// FR-CAT-001: Lấy danh sách danh mục (không kèm chi tiết toàn bộ Recipe để tránh lỗi cột r.Nutrition)
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(cat => cat.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// FR-CAT-002: Lấy thông tin danh mục theo slug
    /// </summary>
    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(cat => cat.Slug == slug, cancellationToken);
    }

    /// <summary>
    /// Kiểm tra tên danh mục đã tồn tại chưa
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AnyAsync(cat => cat.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    /// <summary>
    /// Kiểm tra slug danh mục đã tồn tại chưa
    /// </summary>
    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AnyAsync(cat => cat.Slug == slug, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-003: Tạo mới danh mục
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
            .FirstOrDefaultAsync(cat => cat.Id == id, cancellationToken);
    }

    /// <summary>
    /// Đếm số lượng công thức thuộc danh mục
    /// </summary>
    public async Task<int> GetPublishedRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recipes
            .CountAsync(r => r.CategoryId == categoryId && r.Status == RecipeStatus.Published && !r.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-004: Cập nhật danh mục
    /// </summary>
    public void Update(Category category)
    {
        category.UpdatedAt = DateTime.UtcNow;
        _dbContext.Categories.Update(category);
    }

    /// <summary>
    /// FR-CAT-005: Xóa mềm danh mục
    /// </summary>
    public void Delete(Category category)
    {
        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        _dbContext.Categories.Update(category);
    }
}