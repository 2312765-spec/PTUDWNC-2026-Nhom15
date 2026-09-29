using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.DeleteCategory;

public sealed record DeleteCategoryCommand(Guid Id) : IRequest, ICacheInvalidator
{
    // Quyết định D8: Invalidate cả 2 tags categories và recipes
    public IReadOnlyList<string> TagsToInvalidate => new[] { "categories", "recipes" };
}