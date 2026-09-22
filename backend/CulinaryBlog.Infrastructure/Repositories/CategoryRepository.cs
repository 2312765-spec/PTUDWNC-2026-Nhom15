using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly CulinaryBlogDbContext _context;

    public CategoryRepository(CulinaryBlogDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
{
    return await _context.Categories
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
}
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken);
    }
    
    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AnyAsync(c => c.Slug == slug, cancellationToken);
    }

     public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(category, cancellationToken);
    }
    public async Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .Include(c => c.Recipes)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}