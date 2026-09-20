namespace CulinaryBlog.Application.Common.Models;

/// <summary>
/// Kết quả phân trang offset-based (FR-SRCH-004).
/// pageSize tối đa 50 — vượt thì clamp, không trả lỗi (decisions.md, mục validator).
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public const int MaxPageSize = 50;
    public const int DefaultPageSize = 12;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PagedResult<T> Empty(int page, int pageSize) => new([], 0, page, pageSize);
}
