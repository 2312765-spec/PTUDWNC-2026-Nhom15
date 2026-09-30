using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipes;

public sealed class GetRecipesQueryHandler(
    IRecipeRepository recipeRepository)
    : IRequestHandler<GetRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(GetRecipesQuery request, CancellationToken cancellationToken)
    {
        // FR-SRCH-004: clamp pageSize tối đa 50, page >= 1
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var (recipes, totalCount) = await recipeRepository.GetPagedRecipesAsync(
            request.CategoryId,
            request.Difficulty,
            request.MaxCookTime,
            request.MinServings,
            request.Sort,
            page,
            pageSize,
            cancellationToken);

        var dtos = recipes.Select(r =>
        {
            _ = Guid.TryParse(r.AuthorId, out var authorGuid);
            return new RecipeSummaryDto(
                r.Id,
                r.Title,
                r.Slug,
                r.Description,
                null,
                r.Status.ToString(),
                r.PrepTime,
                r.CookTime,
                r.Difficulty.ToString(),
                authorGuid,
                null,
                r.CreatedAt
            );
        }).ToList();

        return new PagedResult<RecipeSummaryDto>(dtos, totalCount, page, pageSize);
    }
}