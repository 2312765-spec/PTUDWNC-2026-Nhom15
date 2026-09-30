using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipes;

/// <summary>
/// Query danh sách công thức kết hợp FR-RCP-001, FR-SRCH-002 (Lọc), FR-SRCH-003 (Sắp xếp), FR-SRCH-004 (Phân trang).
/// Tuân thủ D8: Cache Redis 15 phút, tag "recipes".
/// </summary>
public sealed record GetRecipesQuery(
    Guid? CategoryId = null,
    string? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = "-createdAt",
    int Page = 1,
    int PageSize = 12
) : IRequest<PagedResult<RecipeSummaryDto>>, ICacheable
{
    // Băm cache key kết hợp tất cả các tham số lọc, sắp xếp, phân trang (D8)
    public string CacheKey => 
        $"recipes:list:c={CategoryId}:d={Difficulty}:mc={MaxCookTime}:ms={MinServings}:s={Sort}:p={Page}:ps={PageSize}";

    public TimeSpan CacheTtl => TimeSpan.FromMinutes(15);
    public IReadOnlyList<string> CacheTags => ["recipes"];
}