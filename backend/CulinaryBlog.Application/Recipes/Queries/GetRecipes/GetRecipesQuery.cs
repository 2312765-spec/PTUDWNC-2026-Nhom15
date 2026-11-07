using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipes;

/// <summary>
/// Query danh sách công thức kết hợp FR-RCP-001, FR-SRCH-002 (Lọc), FR-SRCH-003 (Sắp xếp), FR-SRCH-004 (Phân trang).
/// Tuân thủ D8: Cache Redis 15 phút, tag "recipes". D14: có filter minServings.
/// <para>
/// <see cref="CurrentUserId"/> / <see cref="IsAdmin"/> do endpoint điền từ <c>ICurrentUser</c>,
/// KHÔNG bind từ query string — nếu không client tự xưng Admin để xem Draft của người khác.
/// </para>
/// <para>
/// D81 (/dashboard/recipes): <see cref="Mine"/> = chỉ recipe của người đang đăng nhập (mọi trạng thái, cần đăng nhập —
/// 401); <see cref="Status"/> lọc theo trạng thái, áp SAU bộ lọc quyền nên Guest không xem được Draft qua nó.
/// </para>
/// </summary>
public sealed record GetRecipesQuery(
    Guid? CategoryId = null,
    string? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = "-createdAt",
    int Page = 1,
    int PageSize = PagedResult<RecipeSummaryDto>.DefaultPageSize,
    string? CurrentUserId = null,
    bool IsAdmin = false,
    bool Mine = false,
    string? Status = null
) : IRequest<PagedResult<RecipeSummaryDto>>, ICacheable
{
    // D8: key dạng recipes:list:{hash(query)}. Phạm vi người xem nằm trong hash: Guest, Admin và từng
    // Author thấy tập kết quả khác nhau (FR-RCP-001) — dùng chung key sẽ lộ Draft qua cache.
    public string CacheKey =>
        $"recipes:list:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{ViewerScope}|{CategoryId}|{Difficulty?.Trim().ToLowerInvariant()}|{MaxCookTime}|{MinServings}|{Sort?.Trim().ToLowerInvariant()}|{Page}|{PageSize}|mine:{Mine}|status:{Status?.Trim().ToLowerInvariant()}")))}";

    public TimeSpan CacheTtl => TimeSpan.FromMinutes(15);
    public IReadOnlyList<string> CacheTags => ["recipes"];

    private string ViewerScope => IsAdmin ? "admin" : string.IsNullOrEmpty(CurrentUserId) ? "guest" : $"user:{CurrentUserId}";
}
