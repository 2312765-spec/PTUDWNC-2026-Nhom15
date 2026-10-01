using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Module Công thức — SRS mục 8.3. Chủ sở hữu: B (queries) + C (commands).
///
/// ⚠️ THỨ TỰ ĐĂNG KÝ ROUTE QUAN TRỌNG (D10):
/// "/search" phải đăng ký TRƯỚC "/{slug}", nếu không request tới /recipes/search
/// sẽ khớp vào route slug.
/// </summary>
public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes").WithTags("Recipes");

        // ---- B: queries ----
        // FR-RCP-001, FR-SRCH-002, FR-SRCH-003, FR-SRCH-004: Danh sách công thức có lọc, sắp xếp, phân trang
        group.MapGet("/", async (
            [FromQuery] Guid? categoryId,
            [FromQuery] string? difficulty,
            [FromQuery] int? maxCookTime,
            [FromQuery] int? minServings,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            IRecipeRepository recipeRepository = default!,
            CancellationToken ct = default) =>
        {
            var (items, totalCount) = await recipeRepository.GetPagedRecipesAsync(
                categoryId,
                difficulty,
                maxCookTime,
                minServings,
                sort,
                page,
                pageSize,
                ct);

            var dtos = items.Select(r => new RecipeSummaryDto(
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
            )).ToList();

            var result = new PagedResult<RecipeSummaryDto>(dtos, totalCount, page, pageSize);
            return TypedResults.Ok(result);
        })
        .WithName("GetRecipes")
        .WithSummary("Danh sách — authorization filter + filter/sort/paging (FR-SRCH-002/003/004)")
        .Produces<PagedResult<RecipeSummaryDto>>(StatusCodes.Status200OK);
          
        // FR-SRCH-001: Tìm kiếm toàn văn công thức (BẮT BUỘC ĐỨNG TRƯỚC /{slug})
        group.MapGet("/search", async (
            [AsParameters] SearchRecipesQuery query,
            ISender mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(query, ct);
            return TypedResults.Ok(result);
        })
        .WithName("SearchRecipes")
        .WithSummary("Full-text search tiếng Việt (FR-SRCH-001)")
        .WithDescription("Tìm kiếm công thức nấu ăn bằng từ khóa tiếng Việt có dấu hoặc không dấu.")
        .Produces<PagedResult<RecipeSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
          
        group.MapGet("/{slug}", (string slug) => NotImplementedResults.Pending("FR-RCP-002", "B"))
             .WithSummary("Chi tiết theo slug — Draft/Archived: chỉ owner hoặc Admin (403)");

        // ---- C: commands ----
        group.MapPost("/", () => NotImplementedResults.Pending("FR-RCP-003", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Tạo công thức (Status = Draft) — slug auto-suffix (D10)");

        group.MapPut("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-RCP-004", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Cập nhật — RowVersion mismatch → 409 (D4)");

        group.MapPatch("/{id:guid}/publish", (Guid id) => NotImplementedResults.Pending("FR-RCP-005", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Publish — cần ≥1 step VÀ ≥1 ingredient (D3)");

        group.MapPatch("/{id:guid}/unpublish", (Guid id) => NotImplementedResults.Pending("FR-RCP-005", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Unpublish — Published → Draft");

        group.MapPatch("/{id:guid}/archive", (Guid id) => NotImplementedResults.Pending("FR-RCP-006", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Archive — ẩn khỏi listing công khai");

        group.MapDelete("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-RCP-007", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("SOFT DELETE (D1) — không cascade, không xóa file MinIO");

        // ---- C: steps & ingredients ----
        group.MapPost("/{id:guid}/steps", (Guid id) => NotImplementedResults.Pending("FR-RCP-010", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Thêm bước — server sinh stepNumber, body KHÔNG có stepNumber (D6)");

        group.MapPut("/{id:guid}/steps/{stepId:guid}", (Guid id, Guid stepId) => NotImplementedResults.Pending("FR-RCP-010", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Sửa bước — stepNumber? để đổi vị trí, server renumber lại");

        group.MapDelete("/{id:guid}/steps/{stepId:guid}", (Guid id, Guid stepId) => NotImplementedResults.Pending("FR-RCP-010", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Xóa bước — renumber lại cho liên tục 1,2,3…");

        group.MapPost("/{id:guid}/ingredients", (Guid id) => NotImplementedResults.Pending("FR-RCP-009", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Thêm nguyên liệu — quantity/unit NULLABLE (D7)");

        group.MapPut("/{id:guid}/ingredients/{ingredientId:guid}", (Guid id, Guid ingredientId) => NotImplementedResults.Pending("FR-RCP-009", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Sửa nguyên liệu");

        group.MapDelete("/{id:guid}/ingredients/{ingredientId:guid}", (Guid id, Guid ingredientId) => NotImplementedResults.Pending("FR-RCP-009", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Xóa nguyên liệu");
    }
}