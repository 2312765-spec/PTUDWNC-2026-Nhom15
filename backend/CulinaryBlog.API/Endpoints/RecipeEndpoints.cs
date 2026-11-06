using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.Commands.Create;
using CulinaryBlog.Application.Recipes.Commands.Ingredients;
using CulinaryBlog.Application.Recipes.Commands.Publish;
using CulinaryBlog.Application.Recipes.Commands.Steps;
using CulinaryBlog.Application.Recipes.Commands.Update;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Queries.GetRecipeById;
using CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;
using CulinaryBlog.Application.Recipes.Queries.GetRecipes;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
          
        // D75: chi tiết theo id cho trang sửa (FR-RCP-004 FE) — không cache, luôn có rowVersion mới nhất.
        // Ràng buộc :guid làm route này được ưu tiên hơn /{slug} (D72/D75: slug dạng GUID được thêm "-1").
        group.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
            TypedResults.Ok(await mediator.Send(new GetRecipeByIdQuery(id), ct)))
        .WithName("GetRecipeById")
        .WithSummary("Chi tiết theo id — Draft/Archived: chỉ owner hoặc Admin (403) (D75)")
        .Produces<RecipeDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AllowAnonymous();

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
        // FR-RCP-003. CONS-008: endpoint chỉ nhận request → gửi command → trả kết quả.
        group.MapPost("/", async (
            CreateRecipeCommand command,
            HttpResponse response,
            ISender mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            SetETag(response, result.RowVersion);
            return TypedResults.Created($"/api/v1/recipes/{result.Slug}", result);
        })
             .RequireAuthorization(Policies.Author)
             .WithName("CreateRecipe")
             .WithSummary("Tạo công thức (Status = Draft) — slug auto-suffix (D10)")
             .WithDescription("Body: { title, description, categoryId, prepTime, cookTime, servings, difficulty, instructions?, nutrition?, steps?, ingredients? } (D69). Trả RecipeDetailDto kèm rowVersion + header ETag (D71).")
             .Produces<RecipeDetailDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden);

        // FR-RCP-004. If-Match chuyển nguyên văn vào command — validator kiểm tra (CONS-008, D71).
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRecipeRequest body,
            HttpRequest request,
            HttpResponse response,
            ISender mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateRecipeCommand(
                id, request.Headers.IfMatch.ToString(), body.Title, body.Description, body.CategoryId,
                body.PrepTime, body.CookTime, body.Servings, body.Difficulty, body.Instructions, body.Nutrition);
            var result = await mediator.Send(command, ct);
            SetETag(response, result.RowVersion);
            return TypedResults.Ok(result);
        })
             .RequireAuthorization(Policies.Author)
             .WithName("UpdateRecipe")
             .WithSummary("Cập nhật — header If-Match bắt buộc; RowVersion mismatch → 409 (D4, D71)")
             .WithDescription("Thay toàn bộ thông tin cơ bản (D69), slug không đổi (D13). nutrition vắng/null = giữ nguyên.")
             .Produces<RecipeDetailDto>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        // FR-RCP-005 — D3 (điều kiện publish), D77 (idempotent, If-Match tùy chọn, PublishedAt lần đầu).
        group.MapPatch("/{id:guid}/publish", (Guid id, HttpRequest request, HttpResponse response, ISender mediator, CancellationToken ct) =>
            SendPublishAsync(new PublishRecipeCommand(id, IsPublish: true, NullIfEmpty(request.Headers.IfMatch.ToString())), response, mediator, ct))
             .RequireAuthorization(Policies.Author)
             .WithName("PublishRecipe")
             .WithSummary("Publish — Draft/Archived → Published, cần ≥1 step VÀ ≥1 ingredient (D3); đã Published → 200 idempotent")
             .WithDescription("Header If-Match tùy chọn (D77): có thì phải khớp rowVersion, lệch → 409. Trả RecipeDetailDto + ETag mới.")
             .Produces<RecipeDetailDto>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/unpublish", (Guid id, HttpRequest request, HttpResponse response, ISender mediator, CancellationToken ct) =>
            SendPublishAsync(new PublishRecipeCommand(id, IsPublish: false, NullIfEmpty(request.Headers.IfMatch.ToString())), response, mediator, ct))
             .RequireAuthorization(Policies.Author)
             .WithName("UnpublishRecipe")
             .WithSummary("Unpublish — Published/Archived → Draft; đã Draft → 200 idempotent (D77)")
             .WithDescription("Header If-Match tùy chọn (D77). Trả RecipeDetailDto + ETag mới.")
             .Produces<RecipeDetailDto>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/archive", (Guid id) => NotImplementedResults.Pending("FR-RCP-006", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Archive — ẩn khỏi listing công khai");

        group.MapDelete("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-RCP-007", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("SOFT DELETE (D1) — không cascade, không xóa file MinIO");

        // ---- C: steps & ingredients ----
        // FR-RCP-010 — D6 (server đánh số), D73 (hard delete, lưu 2 pha, khóa dòng recipe).
        group.MapPost("/{id:guid}/steps", async (Guid id, AddStepRequest body, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new AddRecipeStepCommand(id, body.Title, body.Description, body.TimerMinutes, body.ImageUrl), ct);
            return TypedResults.Created((string?)null, result);
        })
             .RequireAuthorization(Policies.Author)
             .WithName("AddRecipeStep")
             .WithSummary("Thêm bước — server sinh stepNumber, body KHÔNG có stepNumber (D6)")
             .WithDescription("Body: { title, description, timerMinutes?, imageUrl? }. Trả RecipeStepDto.")
             .Produces<RecipeStepDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/steps/{stepId:guid}", async (
            Guid id, Guid stepId, UpdateStepRequest body, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new UpdateRecipeStepCommand(id, stepId, body.Title, body.Description, body.TimerMinutes, body.ImageUrl, body.StepNumber), ct);
            return TypedResults.Ok(result);
        })
             .RequireAuthorization(Policies.Author)
             .WithName("UpdateRecipeStep")
             .WithSummary("Sửa bước — stepNumber? để đổi vị trí, server renumber lại")
             .WithDescription("Body: { title, description, timerMinutes?, imageUrl?, stepNumber? }. Trả toàn bộ bước theo thứ tự mới (D73).")
             .Produces<IReadOnlyList<RecipeStepDto>>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/steps/{stepId:guid}", async (Guid id, Guid stepId, ISender mediator, CancellationToken ct) =>
        {
            await mediator.Send(new DeleteRecipeStepCommand(id, stepId), ct);
            return TypedResults.NoContent();
        })
             .RequireAuthorization(Policies.Author)
             .WithName("DeleteRecipeStep")
             .WithSummary("Xóa bước — renumber lại cho liên tục 1,2,3…")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        // FR-RCP-009 — D7 (quantity/unit nullable, orderIndex), D76 (vị trí, hard delete, khóa dòng recipe).
        group.MapPost("/{id:guid}/ingredients", async (Guid id, IngredientRequest body, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new AddRecipeIngredientCommand(id, body.Name, body.Quantity, body.Unit, body.Notes, body.OrderIndex), ct);
            return TypedResults.Created((string?)null, result);
        })
             .RequireAuthorization(Policies.Author)
             .WithName("AddRecipeIngredient")
             .WithSummary("Thêm nguyên liệu — quantity/unit NULLABLE (D7), orderIndex vắng = xếp cuối (D76)")
             .WithDescription("Body: { name, quantity?, unit?, notes?, orderIndex? }. Trả RecipeIngredientDto.")
             .Produces<RecipeIngredientDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/ingredients/{ingredientId:guid}", async (
            Guid id, Guid ingredientId, IngredientRequest body, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new UpdateRecipeIngredientCommand(id, ingredientId, body.Name, body.Quantity, body.Unit, body.Notes, body.OrderIndex), ct);
            return TypedResults.Ok(result);
        })
             .RequireAuthorization(Policies.Author)
             .WithName("UpdateRecipeIngredient")
             .WithSummary("Sửa nguyên liệu — orderIndex? để đổi vị trí, server đánh lại 0..n-1 (D76)")
             .WithDescription("Body: { name, quantity?, unit?, notes?, orderIndex? }. Trả toàn bộ nguyên liệu theo thứ tự mới.")
             .Produces<IReadOnlyList<RecipeIngredientDto>>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/ingredients/{ingredientId:guid}", async (
            Guid id, Guid ingredientId, ISender mediator, CancellationToken ct) =>
        {
            await mediator.Send(new DeleteRecipeIngredientCommand(id, ingredientId), ct);
            return TypedResults.NoContent();
        })
             .RequireAuthorization(Policies.Author)
             .WithName("DeleteRecipeIngredient")
             .WithSummary("Xóa nguyên liệu — hard delete, đánh lại orderIndex (D76)")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>D7/D76 — wire contract của POST và PUT /recipes/{id}/ingredients.</summary>
    public sealed record IngredientRequest(string Name, decimal? Quantity, string? Unit, string? Notes, int? OrderIndex);

    /// <summary>D69 — wire contract của PUT /recipes/{id} (id lấy từ route, RowVersion từ header If-Match).</summary>
    public sealed record UpdateRecipeRequest(
        string Title,
        string Description,
        Guid CategoryId,
        int PrepTime,
        int CookTime,
        int Servings,
        [property: System.Text.Json.Serialization.JsonConverter(typeof(StringOrNumberJsonConverter))] string Difficulty,
        string? Instructions,
        RecipeNutritionInput? Nutrition);

    /// <summary>D6 — wire contract của POST /recipes/{id}/steps (không có stepNumber).</summary>
    public sealed record AddStepRequest(string Title, string Description, int? TimerMinutes, string? ImageUrl);

    /// <summary>D6 — wire contract của PUT /recipes/{id}/steps/{stepId}.</summary>
    public sealed record UpdateStepRequest(string Title, string Description, int? TimerMinutes, string? ImageUrl, int? StepNumber);

    private static async Task<Ok<RecipeDetailDto>> SendPublishAsync(
        PublishRecipeCommand command, HttpResponse response, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        SetETag(response, result.RowVersion);
        return TypedResults.Ok(result);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>D34/D71 — RowVersion mới (base64) trong header ETag, dạng strong ETag có ngoặc kép.</summary>
    private static void SetETag(HttpResponse response, string rowVersion)
    {
        if (!string.IsNullOrEmpty(rowVersion))
        {
            response.Headers.ETag = $"\"{rowVersion}\"";
        }
    }
}