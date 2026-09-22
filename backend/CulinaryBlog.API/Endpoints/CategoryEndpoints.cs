using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Categories.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories")
            .WithTags("Categories");

        // FR-CAT-001: Danh sách danh mục (Guest)
        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetCategoriesQuery());
            return Results.Ok(result);
        }).AllowAnonymous();

        // FR-CAT-002: Chi tiết danh mục theo slug (Guest / Author / Admin)
        group.MapGet("/{slug}", async (
            string slug,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            System.Security.Claims.ClaimsPrincipal user = null!,
            ISender sender = null!) =>
        {
            var currentUserId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var query = new GetCategoryBySlugQuery(slug, page, pageSize, currentUserId);
            var result = await sender.Send(query);

            if (result == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Danh mục với slug '{slug}' không tồn tại."
                );
            }

            return Results.Ok(result);
        }).AllowAnonymous();

        
     // FR-CAT-003: Tạo danh mục mới (Chỉ Admin)
        group.MapPost("/", async (
            [FromBody] CreateCategoryCommand command,
            ISender sender) =>
        {
            var createdCategory = await sender.Send(command);

            return Results.Created(
                $"/api/v1/categories/{createdCategory.Slug}",
                createdCategory
            );
        })
        .WithName("CreateCategory")
        .WithSummary("Tạo danh mục công thức mới (Yêu cầu quyền Admin)")
        .RequireAuthorization("AdminPolicy"); // <-- Bắt buộc xác thực // Hoặc dùng hằng số Policies.Admin
    }
}
