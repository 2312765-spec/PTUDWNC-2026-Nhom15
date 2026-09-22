using CulinaryBlog.Application.Categories.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;

/// <summary>FR-CAT-002. Không yêu cầu xác thực — CurrentUserId null nếu là Guest.</summary>
public sealed record GetCategoryBySlugQuery(
    string Slug,
    int Page = 1,
    int PageSize = 12,
    string? CurrentUserId = null
) : IRequest<CategoryDetailResponseDto>;
