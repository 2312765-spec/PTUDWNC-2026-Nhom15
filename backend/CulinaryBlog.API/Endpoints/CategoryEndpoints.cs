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
     var categories = group.MapGroup("/categories")
                          .WithTags("Categories");

      categories.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetCategoriesQuery(), cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("GetCategories")
        .WithSummary("Xem danh sách tất cả danh mục công thức (FR-CAT-001)")
        .Produces<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)
        .AllowAnonymous();

        return group;
    }
}