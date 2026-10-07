using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.Queries.GetRecipes;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;
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
        // FR-RCP-001 + FR-SRCH-002/003/004. Tham số khai báo tường minh (không [AsParameters]) để
        // CurrentUserId/IsAdmin chỉ đến từ token, client không bind được qua query string.
        group.MapGet("/", async (
            Guid? categoryId,
            string? difficulty,
            int? maxCookTime,
            int? minServings,
            string? sort,
            int? page,
            int? pageSize,
            ICurrentUser currentUser,
            ISender mediator,
            CancellationToken ct) =>
        {
            var query = new GetRecipesQuery(
                categoryId, difficulty, maxCookTime, minServings, sort ?? "-createdAt",
                page ?? 1, pageSize ?? PagedResult<RecipeSummaryDto>.DefaultPageSize,
                CurrentUserId: currentUser.UserId, IsAdmin: currentUser.IsAdmin);
            var result = await mediator.Send(query, ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetRecipes")
        .WithSummary("Danh sách — authorization filter + filter/sort/paging (FR-SRCH-002/003/004)")
        .WithDescription("Guest thấy Published; Author thấy thêm Draft/Archived của mình; Admin thấy tất cả.")
        .Produces<PagedResult<RecipeSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .AllowAnonymous();
          
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
          
        // FR-RCP-002: Xem chi tiết công thức nấu ăn theo slug
        group.MapGet("/{slug}", async (
            string slug,
            ISender mediator,
            CancellationToken ct) =>
        {
            var query = new GetRecipeBySlugQuery(slug);
            var result = await mediator.Send(query, ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetRecipeBySlug")
        .WithSummary("Chi tiết theo slug — Draft/Archived: chỉ owner hoặc Admin (403)")
        .WithDescription("Trả về thông tin chi tiết công thức kèm steps, ingredients, images, nutrition, category, author.")
        .Produces<RecipeDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .AllowAnonymous();

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