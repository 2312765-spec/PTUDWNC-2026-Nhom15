using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> HasRecipesAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<int> GetTotalRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<int> GetPublishedRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    void Update(Category category);
    void Delete(Category category);
}