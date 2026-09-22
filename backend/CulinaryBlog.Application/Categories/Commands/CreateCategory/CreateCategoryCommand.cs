using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces; // Nơi chứa ICacheInvalidator
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(
    string Name,
    string? Description
) : IRequest<CategoryDto>, ICacheInvalidator
{
    // D8: Invalidate tag "categories" và "recipes"
    public IReadOnlyList<string> TagsToInvalidate => ["categories", "recipes"];
}