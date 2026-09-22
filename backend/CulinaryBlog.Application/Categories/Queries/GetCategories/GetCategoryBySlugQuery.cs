using CulinaryBlog.Application.Categories.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;

public record GetCategoryBySlugQuery(
    string Slug,
    int Page = 1,
    int PageSize = 12,
    string? CurrentUserId = null
) : IRequest<CategoryDetailResponseDto?>;