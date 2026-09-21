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

    public async Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .Include(c => c.Recipes)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}