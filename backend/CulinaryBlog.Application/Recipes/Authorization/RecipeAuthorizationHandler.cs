using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Recipes.Authorization;

/// <summary>
/// NFR-SEC-006 + docs/permissions.md mục 3.1/3.2 — phân quyền resource-based cho Recipe và mọi
/// entity con (step, ingredient, image), kiểm tra ở Application Layer (endpoint vẫn chặn trước
/// bằng <c>AuthorPolicy</c>). Quyết định D70: hiện thực bằng <see cref="ICurrentUser"/> thay cho
/// <c>IAuthorizationService</c> của ASP.NET Core, để Application không phụ thuộc ASP.NET.
/// </summary>
public sealed class RecipeAuthorizationHandler(ICurrentUser currentUser)
{
    /// <summary>FR-RCP-003 — chỉ Author hoặc Admin được tạo recipe.</summary>
    public void EnsureCanCreate()
    {
        EnsureAuthenticated();

        if (!currentUser.IsAdmin && !currentUser.IsInRole(Roles.Author))
        {
            throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền tạo công thức.");
        }
    }

    /// <summary>
    /// FR-RCP-004/005/006/007/009/010 — chỉ tác giả sở hữu (<c>AuthorId == currentUserId</c>) hoặc
    /// Admin được sửa recipe và mọi entity con của nó. Admin bypass ownership (permissions.md mục 2).
    /// </summary>
    public void EnsureCanModify(Recipe recipe)
    {
        EnsureAuthenticated();

        if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId)
        {
            throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền sửa công thức này.");
        }
    }

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId))
        {
            throw new UnauthorizedException(ErrorCodes.AuthTokenInvalid, "Bạn cần đăng nhập.");
        }
    }
}
