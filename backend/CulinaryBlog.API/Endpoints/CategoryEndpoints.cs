using System.Security.Claims;
using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Module Quản lý Danh mục (FR-CAT) - Minimal APIs
/// Tuân thủ CONS-003: Không dùng MVC Controller.
/// </summary>
public static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        // FR-CAT-001: Xem Danh sách Tất cả Danh mục
        // Public endpoint - Không yêu cầu xác thực
        // Trả về danh sách tất cả danh mục kèm số lượng công thức Published, sắp xếp Name tăng dần
        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetCategoriesQuery();
            var result = await sender.Send(query, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("GetCategories")
        .WithSummary("Xem danh sách tất cả danh mục công thức (FR-CAT-001)")
        .WithDescription("Trả về danh sách danh mục kèm số lượng công thức đã xuất bản (Published), cache qua Redis với key 'categories:all' (TTL 30 phút, tag 'categories'), sắp xếp theo tên tăng dần.")
        .Produces<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)
        .AllowAnonymous();

        // FR-CAT-002: Xem Chi tiết Danh mục & Công thức phân trang
        // Public endpoint - Lấy UserId từ ClaimsPrincipal để Author xem được Draft của chính mình
        group.MapGet("/{slug}", async (
            string slug, 
            int? page, 
            int? pageSize, 
            ClaimsPrincipal user, 
            ISender sender, 
            CancellationToken cancellationToken) =>
        {
            var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = new GetCategoryBySlugQuery(slug, page ?? 1, pageSize ?? 12, CurrentUserId: currentUserId);
            var result = await sender.Send(query, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("GetCategoryBySlug")
        .WithSummary("Xem chi tiết danh mục và công thức phân trang (FR-CAT-002)")
        .WithDescription("Trả về thông tin chi tiết danh mục kèm danh sách công thức phân trang ở trạng thái Published, Author thấy thêm Draft của chính mình.")
        .Produces<CategoryDetailResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AllowAnonymous();

        // FR-CAT-003: Tạo mới Danh mục (Admin)
        // Yêu cầu quyền Quản trị viên -> Chưa đăng nhập = 401, không phải Admin = 403
        group.MapPost("/", async (CreateCategoryCommand command, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);
            return TypedResults.Created($"/api/v1/categories/{result.Slug}", result);
        })
        .WithName("CreateCategory")
        .WithSummary("Tạo danh mục món ăn mới (FR-CAT-003)")
        .WithDescription("Tạo danh mục mới, tự động sinh slug URL-friendly duy nhất theo Quyết định D10. Yêu cầu quyền Admin.")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin)) // <-- SỬA LỖI 201 THÀNH 401/403 Ở ĐÂY
        .Produces<CategoryDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // FR-CAT-004: Cập nhật Danh mục (Admin)
        // Yêu cầu quyền Quản trị viên -> Chưa đăng nhập = 401, không phải Admin = 403
        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateCategoryCommand(id, request.Name, request.Description);
            var result = await sender.Send(command, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("UpdateCategory")
        .WithSummary("Cập nhật thông tin danh mục (FR-CAT-004)")
        .WithDescription("Cập nhật Tên và Mô tả của danh mục. Theo Quyết định D10, slug được giữ nguyên để không làm gãy liên kết SEO.")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin)) // <-- SỬA LỖI 404 THÀNH 401/403 Ở ĐÂY
        .Produces<CategoryDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}

public sealed record UpdateCategoryRequest(string Name, string? Description);