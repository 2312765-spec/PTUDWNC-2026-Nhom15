using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;

/// <summary>
/// Query lấy thông tin chi tiết danh mục và danh sách công thức phân trang (SRS FR-CAT-002)
/// </summary>
public record GetCategoryBySlugQuery(
    string Slug,
    int Page = 1,
    int PageSize = 12
) : IRequest<CategoryDetailDto>;

public class GetCategoryBySlugQueryHandler : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCategoryBySlugQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<CategoryDetailDto> Handle(
        GetCategoryBySlugQuery request, 
        CancellationToken cancellationToken)
    {
        var slug = request.Slug?.Trim().ToLowerInvariant() ?? string.Empty;

        // 1. Handler tìm category theo slug: _unitOfWork.Categories.GetBySlugAsync(slug) (SRS FR-CAT-002 Luồng chính bước 3)
        var category = await _unitOfWork.Categories.GetBySlugAsync(slug, cancellationToken);

        // Ngoại lệ A1 – Slug không tồn tại: HTTP 404 Not Found với RFC 7807 body
        if (category is null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với slug '{slug}'.");
        }

        // 2. Chuẩn hóa phân trang
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        // 3. Query recipes thuộc category với Status == Published (+ Draft của currentUser nếu là Author, hoặc tất cả nếu là Admin)
        // (SRS FR-CAT-002 Luồng chính bước 4)
        var (recipes, totalCount) = await _unitOfWork.Categories.GetRecipesByCategoryAsync(
            category.Id,
            _currentUserService.UserId,
            _currentUserService.IsAdmin,
            page,
            pageSize,
            cancellationToken
        );

        // 4. Map sang RecipeSummaryDto
        var recipeSummaryDtos = recipes.Select(r => new RecipeSummaryDto(
            r.Id,
            r.Title,
            r.Slug,
            r.Description,
            r.AuthorId,
            r.AuthorName,
            r.AuthorAvatar,
            r.Status.ToString(),
            r.Difficulty,
            r.PrepTimeMinutes,
            r.CookTimeMinutes,
            r.Servings,
            r.ThumbnailUrl,
            r.LikeCount,
            r.CreatedAt
        )).ToList();

        // 5. Đếm số món Published cho CategoryDto
        var pagedRecipes = new PagedResult<RecipeSummaryDto>(recipeSummaryDtos, totalCount, page, pageSize);

        var categoryDto = new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.ImageUrl,
            totalCount, // Hoặc tổng số món published
            category.OrderIndex
        );

        return new CategoryDetailDto(categoryDto, pagedRecipes);
    }
}
