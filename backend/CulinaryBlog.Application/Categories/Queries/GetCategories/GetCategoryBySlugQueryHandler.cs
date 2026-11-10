using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;

/// <summary>
/// FR-CAT-002. Guest chỉ thấy Published; chủ sở hữu thấy thêm Draft của chính mình.
/// Slug không tồn tại → NotFoundException (CONS-008: quyết định 404 nằm ở Application,
/// không phải ở endpoint — GlobalExceptionMiddleware lo phần map sang RFC 7807).
/// NFR-PERF-004: lọc quyền, đếm và phân trang chạy trong database — chỉ nạp recipe của trang hiện tại.
/// </summary>
public sealed class GetCategoryBySlugQueryHandler : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailResponseDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRecipeRepository _recipeRepository;

    public GetCategoryBySlugQueryHandler(ICategoryRepository categoryRepository, IRecipeRepository recipeRepository)
    {
        _categoryRepository = categoryRepository;
        _recipeRepository = recipeRepository;
    }

    public async Task<CategoryDetailResponseDto> Handle(
        GetCategoryBySlugQuery request,
        CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetBySlugAsync(request.Slug, cancellationToken);
        if (category is null)
        {
            throw new NotFoundException(ErrorCodes.CategoryNotFound, $"Không tìm thấy danh mục với slug '{request.Slug}'.");
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1
            ? PagedResult<RecipeSummaryDto>.DefaultPageSize
            : Math.Min(request.PageSize, PagedResult<RecipeSummaryDto>.MaxPageSize);

        // Guest chỉ thấy Published; người đăng nhập thấy thêm Draft của chính mình (không Archived).
        var (recipes, totalCount) = await _recipeRepository.GetPagedByCategoryIdAsync(
            category.Id, request.CurrentUserId, page, pageSize, cancellationToken);

        var items = recipes
            .Select(r =>
            {
                var featured = r.Images.FirstOrDefault(i => i.IsPrimary) ?? r.Images.FirstOrDefault();
                return new RecipeSummaryDto(
                    r.Id,
                    r.Title,
                    r.Slug,
                    r.Description,
                    featured?.OriginalUrl,
                    r.Status.ToString(),
                    r.PrepTime,
                    r.CookTime,
                    r.Difficulty.ToString(),
                    Guid.TryParse(r.AuthorId, out var authorGuid) ? authorGuid : Guid.Empty,
                    null,
                    r.CreatedAt,
                    FeaturedImageCardUrl: featured?.CardUrl);
            })
            .ToList();

        var pagedRecipes = new PagedResult<RecipeSummaryDto>(items, totalCount, page, pageSize);

        var categoryDto = new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            totalCount);

        return new CategoryDetailResponseDto(categoryDto, pagedRecipes);
    }
}
