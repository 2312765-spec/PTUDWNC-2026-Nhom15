using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Categories.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// Query tìm kiếm toàn văn công thức (FR-SRCH-001).
/// Tuân thủ D8: Cache TTL 1 phút, tag "recipes".
/// Khớp chính xác interface ICacheable: CacheTtl kiểu TimeSpan (không nullable).
/// </summary>
public sealed record SearchRecipesQuery(
    string? Q = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<PagedResult<RecipeSummaryDto>>, ICacheable
{
    public string CacheKey => $"recipes:search:{(Q ?? "").Trim().ToLowerInvariant()}:p{Page}:s{PageSize}";
    public TimeSpan CacheTtl => TimeSpan.FromMinutes(1);
    public IReadOnlyList<string> CacheTags => ["recipes"];
}