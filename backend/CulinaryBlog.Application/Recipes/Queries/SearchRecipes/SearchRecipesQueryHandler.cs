using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public sealed class SearchRecipesQueryHandler(
    IRecipeRepository recipeRepository,
    IIdentityService identityService)
    : IRequestHandler<SearchRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(
        SearchRecipesQuery request,
        CancellationToken cancellationToken)
    {
        var rawQuery = (request.Q ?? string.Empty).Trim();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, PagedResult<RecipeSummaryDto>.MaxPageSize);

        var (hits, totalCount) = await recipeRepository.SearchPublishedRecipesAsync(
            rawQuery,
            page,
            pageSize,
            cancellationToken);

        // 1. Gom danh sách AuthorId của trang kết quả (loại bỏ trùng lặp và rỗng)
        var authorIds = hits
            .Select(hit => hit.Recipe.AuthorId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        // 2. Chỉ gọi IIdentityService khi có ít nhất 1 tác giả (tránh query thừa khi hits rỗng)
        var authorNames = authorIds.Count > 0
            ? await identityService.GetDisplayNamesAsync(authorIds, cancellationToken)
            : null;

        // 3. Map sang RecipeSummaryDto với tên tác giả thực tế (hoặc null nếu không có)
        var dtos = hits.Select(hit =>
        {
            var r = hit.Recipe;
            var primaryImage = r.Images.FirstOrDefault(i => i.IsPrimary)
                ?? r.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();

            _ = Guid.TryParse(r.AuthorId, out var authorGuid);

            string? authorName = null;
            if (authorNames is not null && !string.IsNullOrWhiteSpace(r.AuthorId))
            {
                authorNames.TryGetValue(r.AuthorId, out authorName);
            }

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
                authorName,
                r.CreatedAt,
                hit.Rank);
        }).ToList();

        return new PagedResult<RecipeSummaryDto>(dtos, totalCount, page, pageSize);
    }
}