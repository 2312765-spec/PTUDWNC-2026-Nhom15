using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipes;

public sealed class GetRecipesQueryHandler(
    IRecipeRepository recipeRepository)
    : IRequestHandler<GetRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(GetRecipesQuery request, CancellationToken cancellationToken)
    {
        // FR-SRCH-004: clamp pageSize tối đa 50 (decisions.md: vượt 50 → clamp, không trả lỗi)
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, PagedResult<RecipeSummaryDto>.MaxPageSize);

        // FR-RCP-001 / NFR-SEC-006: authorization filter ở Application layer.
        // Guest → chỉ Published · Author → thêm Draft/Archived của chính mình · Admin → tất cả.
        var includeAllStatuses = request.IsAdmin;
        var nonPublishedOwnerId = includeAllStatuses ? null : request.CurrentUserId;

        var (recipes, totalCount) = await recipeRepository.GetPagedRecipesAsync(
            request.CategoryId,
            ParseDifficulty(request.Difficulty),
            request.MaxCookTime,
            request.MinServings,
            request.Sort,
            includeAllStatuses,
            nonPublishedOwnerId,
            page,
            pageSize,
            cancellationToken);

        var dtos = recipes.Select(r =>
        {
            var primaryImage = r.Images.FirstOrDefault(i => i.IsPrimary) ?? r.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();
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
                null,
                r.CreatedAt
            );
        }).ToList();

        return new PagedResult<RecipeSummaryDto>(dtos, totalCount, page, pageSize);
    }

    // Validator đã chặn giá trị lạ (D14: Easy|Medium|Hard|Expert, hoặc 1–4) — ở đây chỉ chuyển kiểu.
    private static RecipeDifficulty? ParseDifficulty(string? difficulty) =>
        string.IsNullOrWhiteSpace(difficulty)
            ? null
            : Enum.TryParse<RecipeDifficulty>(difficulty.Trim(), ignoreCase: true, out var d) && Enum.IsDefined(d) ? d : null;
}
