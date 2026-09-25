using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;

/// <summary>
/// FR-CAT-002. Guest chỉ thấy Published; chủ sở hữu thấy thêm Draft của chính mình.
/// Slug không tồn tại → NotFoundException (CONS-008: quyết định 404 nằm ở Application,
/// không phải ở endpoint — GlobalExceptionMiddleware lo phần map sang RFC 7807).
/// </summary>
public sealed class GetCategoryBySlugQueryHandler : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailResponseDto>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryBySlugQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
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

        var visibleRecipes = category.Recipes
            .Where(r => !r.IsDeleted)
            .Where(r => r.Status == RecipeStatus.Published
                || (!string.IsNullOrEmpty(request.CurrentUserId) && r.Status == RecipeStatus.Draft && r.AuthorId == request.CurrentUserId))
            .ToList();

        var totalCount = visibleRecipes.Count;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1
            ? PagedResult<RecipeSummaryDto>.DefaultPageSize
            : Math.Min(request.PageSize, PagedResult<RecipeSummaryDto>.MaxPageSize);

        var items = visibleRecipes
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RecipeSummaryDto(
                r.Id,
                r.Title,
                r.Slug,
                r.Description,
                r.Images.FirstOrDefault(i => i.IsPrimary)?.OriginalUrl ?? r.Images.FirstOrDefault()?.OriginalUrl,
                r.Status.ToString(),
                r.PrepTime,
                r.CookTime,
                r.Difficulty.ToString(),
                Guid.TryParse(r.AuthorId, out var authorGuid) ? authorGuid : Guid.Empty,
                null,
                r.CreatedAt))
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
