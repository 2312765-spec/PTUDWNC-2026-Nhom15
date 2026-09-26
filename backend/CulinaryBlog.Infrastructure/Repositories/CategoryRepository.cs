using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

/// <summary>
/// Triển khai ICategoryRepository sử dụng Entity Framework Core 10.
/// Tầng Infrastructure thực thi interface của Tầng Domain (Clean Architecture).
/// </summary>
public class CategoryRepository : ICategoryRepository
{
    private readonly CulinaryBlogDbContext _dbContext;
   
    public Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Recipe?>(null);
    }
    public CategoryRepository(CulinaryBlogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// FR-CAT-001: Lấy tất cả danh mục kèm danh sách công thức.
    /// Tự động loại bỏ danh mục bị soft delete nhờ Global Query Filter (!IsDeleted).
    /// Lọc recipe ở trạng thái Published và sắp xếp theo Name tăng dần (A-Z).
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Include(c => c.Recipes)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// FR-CAT-002: Lấy danh mục theo Slug.
    /// </summary>
    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Include(c => c.Recipes)
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
    }

    /// <summary>
    /// Kiểm tra xem tên danh mục đã tồn tại chưa.
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    /// <summary>
    /// Kiểm tra xem Slug danh mục đã tồn tại chưa.
    /// </summary>
    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AnyAsync(c => c.Slug == slug, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-003: Thêm danh mục mới vào DbContext.
    /// </summary>
    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _dbContext.Categories.AddAsync(category, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-004: Lấy Category theo ID.
    /// Không dùng AsNoTracking() để Entity Framework theo dõi thay đổi khi Update.
    /// </summary>
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <summary>
    /// Đếm số công thức đã xuất bản (Published) thuộc danh mục.
    /// </summary>
    public async Task<int> GetPublishedRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recipes
            .CountAsync(r => r.CategoryId == categoryId && r.Status == RecipeStatus.Published && !r.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// FR-CAT-004: Cập nhật thông tin danh mục.
    /// </summary>
    public void Update(Category category)
    {
        category.UpdatedAt = DateTime.UtcNow;
        _dbContext.Categories.Update(category);
    }

    /// <summary>
    /// FR-CAT-005: Xóa mềm danh mục (Soft Delete theo Quyết định D2).
    /// </summary>
    public void Delete(Category category)
    {
        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        _dbContext.Categories.Update(category);
    }
    
}
