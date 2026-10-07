using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;

/// <summary>
/// FR-RCP-002: Query lấy chi tiết công thức kèm cache 5 phút và tag ["recipes", $"recipe:{slug}"].
/// </summary>
public sealed record GetRecipeBySlugQuery(string Slug) : IRequest<RecipeDetailDto>, ICacheable
{
    public string CacheKey => $"recipe:{Slug}";
    public TimeSpan CacheTtl => TimeSpan.FromMinutes(5);
    public IReadOnlyList<string> CacheTags => ["recipes", $"recipe:{Slug}"];
}