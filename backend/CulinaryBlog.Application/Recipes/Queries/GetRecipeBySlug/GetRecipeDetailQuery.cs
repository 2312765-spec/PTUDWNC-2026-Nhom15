using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;

/// <summary>
/// FR-RCP-002 — dữ liệu chi tiết công thức, cache Redis theo D8: key <c>recipe:{slug}</c>, TTL 5 phút,
/// tag <c>recipes</c> + <c>recipe:{slug}</c>. KHÔNG kiểm tra quyền — chỉ gọi qua
/// <see cref="GetRecipeBySlugQueryHandler"/>, không map ra endpoint.
/// </summary>
public sealed record GetRecipeDetailQuery(string Slug) : IRequest<RecipeDetailDto>, ICacheable
{
    public string CacheKey => $"recipe:{Slug}";

    public TimeSpan CacheTtl => TimeSpan.FromMinutes(5);

    public IReadOnlyList<string> CacheTags => ["recipes", $"recipe:{Slug}"];
}
