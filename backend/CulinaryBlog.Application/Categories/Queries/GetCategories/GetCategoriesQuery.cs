using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>, ICacheable
{
    public string CacheKey => "categories:all";
    public TimeSpan CacheTtl => TimeSpan.FromMinutes(30);
    public IReadOnlyList<string> CacheTags => ["categories"];
}