using System.Security.Claims;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.API.Extensions;

/// <summary>Hiện thực <see cref="ICurrentUser"/> đọc từ HttpContext. Chủ sở hữu: A.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin => IsInRole(Roles.Admin);

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
