using CulinaryBlog.Application.Categories.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description = null
) : IRequest<CategoryDto>
{
    // Bổ sung thuộc tính này để khớp với UnitTests
    public IReadOnlyList<string> TagsToInvalidate => new[] { "categories", "recipes" };


}