namespace CulinaryBlog.Application.Categories.DTOs;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int RecipeCount
);