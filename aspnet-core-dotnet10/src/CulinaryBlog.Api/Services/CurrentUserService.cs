using System.Security.Claims;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.WebApi.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => 
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? 
        User?.FindFirstValue("sub") ?? 
        _httpContextAccessor.HttpContext?.Request.Headers["x-user-id"].FirstOrDefault();

    public string? Role => 
        User?.FindFirstValue(ClaimTypes.Role) ?? 
        _httpContextAccessor.HttpContext?.Request.Headers["x-user-role"].FirstOrDefault();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true || !string.IsNullOrEmpty(UserId);
}
