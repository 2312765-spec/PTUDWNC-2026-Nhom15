using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public sealed class SearchRecipesQueryHandler(
    IRecipeRepository recipeRepository, IIdentityService? identityService=null)
    : IRequestHandler<SearchRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(SearchRecipesQuery request, CancellationToken cancellationToken)
    {
        var rawQuery = (request.Q ?? string.Empty).Trim();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, PagedResult<RecipeSummaryDto>.MaxPageSize);

        var (hits, totalCount) = await recipeRepository.SearchPublishedRecipesAsync(
            rawQuery,
            page,
            pageSize,
            cancellationToken);

         // 1. Gom danh sách AuthorId của trang kết quả hiện tại (loại bỏ trùng lặp và rỗng)
        var authorIds = hits
            .Select(hit => hit.Recipe.AuthorId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        // 2. Lấy dictionary tên tác giả qua IIdentityService (FR-RCP-002)
        // Thêm kiểm tra "identityService != null" để triệt tiêu hoàn toàn cảnh báo CS8602
        var authorNames = (identityService != null && authorIds.Count > 0)
            ? await identityService.GetDisplayNamesAsync(authorIds, cancellationToken)
            : new Dictionary<string, string>();
        

        // FR-SRCH-001/D49: repository đã xếp theo ts_rank — giữ nguyên thứ tự, trả kèm điểm.
        var dtos = hits.Select(hit =>
        {
            var r = hit.Recipe;

            // Lấy ảnh đại diện (Primary hoặc ảnh đầu tiên)
            var primaryImage = r.Images.FirstOrDefault(i => i.IsPrimary) ?? r.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();
            
            // Xử lý convert AuthorId từ string sang Guid an toàn 
            _ = Guid.TryParse(r.AuthorId, out var authorGuid);
            // 3. Map tên tác giả từ dictionary (nếu không có thì trả về null)
            string? authorName = null;
            if (!string.IsNullOrEmpty(r.AuthorId) && authorNames.TryGetValue(r.AuthorId, out var name))
            {
                authorName = name;
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
                hit.Rank
            );
        }).ToList();

        return new PagedResult<RecipeSummaryDto>(dtos, totalCount, page, pageSize);
    
    }
    
}