using CulinaryBlog.Application.Categories.DTOs;
using MediatR;
using CulinaryBlog.Application.Common.Interfaces;
namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

/// <summary>
/// FR-CAT-003: Command tạo mới Danh mục món ăn.
/// Yêu cầu quyền Quản trị viên (Admin).
/// </summary>
public sealed record CreateCategoryCommand(
    string Name,
    string? Description = null
) : IRequest<CategoryDto>, ICacheInvalidator
{
    // D8: Bắt buộc xóa cả 2 tag "categories" và "recipes"
    public IReadOnlyList<string> TagsToInvalidate => ["categories", "recipes"];
}
