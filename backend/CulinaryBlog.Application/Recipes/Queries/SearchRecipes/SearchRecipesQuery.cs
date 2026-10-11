using System.Security.Cryptography;
using System.Text;
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
    int PageSize = PagedResult<RecipeSummaryDto>.DefaultPageSize
) : IRequest<PagedResult<RecipeSummaryDto>>, ICacheable
{
    // D8: key dạng recipes:search:{hash} — không nhét nguyên chuỗi người dùng gõ vào key Redis.
    public string CacheKey =>
        $"recipes:search:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{(Q ?? "").Trim().ToLowerInvariant()}|{Page}|{PageSize}")))}";
    public TimeSpan CacheTtl => TimeSpan.FromMinutes(1);
    public IReadOnlyList<string> CacheTags => ["recipes"];
}