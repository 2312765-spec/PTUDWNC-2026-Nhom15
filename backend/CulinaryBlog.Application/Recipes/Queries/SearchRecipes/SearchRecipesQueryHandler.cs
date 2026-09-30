using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public sealed class SearchRecipesQueryHandler(
    IRecipeRepository recipeRepository)
    : IRequestHandler<SearchRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(SearchRecipesQuery request, CancellationToken cancellationToken)
    {
        var rawQuery = (request.Q ?? string.Empty).Trim();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, PagedResult<RecipeSummaryDto>.MaxPageSize);

        var (recipes, totalCount) = await recipeRepository.SearchPublishedRecipesAsync(
            rawQuery,
            page,
            pageSize,
            cancellationToken);

        var dtos = recipes.Select(r =>
        {
            // Lấy ảnh đại diện (Primary hoặc ảnh đầu tiên)
            var primaryImage = r.Images.FirstOrDefault(i => i.IsPrimary) ?? r.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();
            
            // Xử lý convert AuthorId từ string sang Guid an toàn (ảnh 5)
            _ = Guid.TryParse(r.AuthorId, out var authorGuid);

            return new RecipeSummaryDto(
                r.Id,
                r.Title,
                r.Slug,
                r.Description,
                primaryImage?.OriginalUrl,
                r.Status.ToString(),
                r.PrepTime,
                r.CookTime,
                r.Difficulty.ToString(),
                authorGuid,
                null, // AuthorName (nullable theo ảnh 5)
                r.CreatedAt
            );
        }).ToList();

        return new PagedResult<RecipeSummaryDto>(dtos, totalCount, page, pageSize);
    }
}