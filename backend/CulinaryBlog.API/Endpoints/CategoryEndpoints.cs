using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Categories.Queries.GetCategories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetCategoriesQuery();
            var result = await sender.Send(query, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("GetCategories")
        .WithSummary("Xem danh sách tất cả danh mục công thức (FR-CAT-001)")
        .Produces<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)
        .AllowAnonymous();

        return group;
    }
}