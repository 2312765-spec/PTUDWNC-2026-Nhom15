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
    public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => 
        Task.FromResult<Recipe?>(null);

    public Task<Recipe?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => 
        Task.FromResult<Recipe?>(null);

    // Chữ ký chuẩn theo interface (trả về Task.FromResult<dynamic>)
    public Task<dynamic> GetPagedByCategoryIdAsync(Guid categoryId, RecipeStatus? status, int page, int pageSize, CancellationToken cancellationToken = default) => 
        throw new NotImplementedException();

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default) => 
        Task.FromResult(false);

    public Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default) => 
        Task.CompletedTask;

    public void Update(Recipe recipe) { }

    public void Delete(Recipe recipe) { }

    public async Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default) =>
        await context.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> IRecipeRepository.GetPagedByCategoryIdAsync(Guid categoryId, RecipeStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}