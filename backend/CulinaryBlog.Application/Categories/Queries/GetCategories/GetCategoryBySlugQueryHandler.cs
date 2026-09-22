using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;

public sealed class GetCategoryBySlugQueryHandler : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailResponseDto?>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryBySlugQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryDetailResponseDto?> Handle(
        GetCategoryBySlugQuery request, 
        CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetBySlugAsync(request.Slug, cancellationToken);
        if (category == null)
        {
            return null;
        }

        var recipesQuery = category.Recipes
            .Where(r => !r.IsDeleted)
            .Where(r => r.Status == RecipeStatus.Published 
                || (!string.IsNullOrEmpty(request.CurrentUserId) && r.Status == RecipeStatus.Draft && r.AuthorId == request.CurrentUserId));

        var totalCount = recipesQuery.Count();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? PagedResult<RecipeSummaryDto>.DefaultPageSize : Math.Min(request.PageSize, PagedResult<RecipeSummaryDto>.MaxPageSize);

        var items = recipesQuery
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
                r.CreatedAt
            ))
            .ToList();

        var pagedRecipes = new PagedResult<RecipeSummaryDto>(
            items,
            totalCount,
            page,
            pageSize
        );

        var categoryDto = new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            totalCount
        );

        return new CategoryDetailResponseDto(categoryDto, pagedRecipes);
    }
}