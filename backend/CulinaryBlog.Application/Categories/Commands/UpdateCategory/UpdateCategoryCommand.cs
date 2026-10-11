using CulinaryBlog.Application.Categories.DTOs;
using MediatR;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description = null
) : IRequest<CategoryDto>,ICacheInvalidator

{
    // D8: Bắt buộc xóa cả 2 tag "categories" và "recipes" vì Recipe DTO có nhúng Category Name
    public IReadOnlyList<string> TagsToInvalidate => ["categories", "recipes"];
}