using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Module Danh mục — SRS mục 8.2. Chủ sở hữu: <b>B</b>. Slice S3.
///
/// Quyết định bắt buộc: D8 (Redis, categories:all TTL 30p, invalidate CẢ tag "recipes"),
/// D2 (soft delete), D10 (slug auto-suffix).
/// </summary>
public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories").WithTags("Categories");

        group.MapGet("/", GetCategoriesAsync)
             .WithName("GetCategories")
             .WithSummary("Danh sách danh mục kèm recipeCount — cache 30 phút")
             .Produces<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)
             .AllowAnonymous();

        group.MapGet("/{slug}", GetCategoryBySlugAsync)
             .WithName("GetCategoryBySlug")
             .WithSummary("Chi tiết danh mục + recipes phân trang — Guest chỉ thấy Published")
             .Produces<CategoryDetailResponseDto>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .AllowAnonymous();

        group.MapPost("/", CreateCategoryAsync)
             .WithName("CreateCategory")
             .WithSummary("[Admin] Tạo danh mục — slug trùng thì auto-suffix (D10)")
             .Produces<CategoryDto>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status409Conflict)
             .RequireAuthorization(Policies.Admin);

        group.MapPut("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-CAT-004", "B"))
             .RequireAuthorization(Policies.Admin)
             .WithSummary("[Admin] Cập nhật danh mục — Slug KHÔNG đổi khi đổi Name");

        group.MapDelete("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-CAT-005", "B"))
             .RequireAuthorization(Policies.Admin)
             .WithSummary("[Admin] Soft delete — còn recipe thì 409 (D2)");
    }

    /// <summary>CONS-008: endpoint chỉ nhận request → gửi query → trả kết quả.</summary>
    private static async Task<IResult> GetCategoriesAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetCategoriesQuery(), ct);
        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Không yêu cầu đăng nhập — <see cref="ICurrentUser.UserId"/> null nếu là Guest, handler
    /// tự lọc Draft theo D2/FR-CAT-002. Slug không tồn tại ném NotFoundException (không xử lý
    /// null ở đây) — GlobalExceptionMiddleware map sang 404 RFC 7807 với đúng mã lỗi.
    /// </summary>
    private static async Task<IResult> GetCategoryBySlugAsync(
        string slug,
        ICurrentUser currentUser,
        ISender sender,
        CancellationToken ct,
        int page = 1,
        int pageSize = 12)
    {
        var query = new GetCategoryBySlugQuery(slug, page, pageSize, currentUser.UserId);
        var result = await sender.Send(query, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> CreateCategoryAsync(CreateCategoryCommand command, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return TypedResults.Created($"/api/v1/categories/{result.Slug}", result);
    }
}
