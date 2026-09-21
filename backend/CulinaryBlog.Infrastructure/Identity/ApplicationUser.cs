using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Identity;

/// <summary>
/// SRS 7.7. D23/ADR-0003: đặt ở Infrastructure (không phải Domain) vì kế thừa
/// <see cref="IdentityUser{TKey}"/> — Domain phải 0 package reference (CONS-001).
/// Application không bao giờ tham chiếu trực tiếp lớp này — chỉ qua IIdentityService.
/// D5: KHÔNG có FullName/UserName tùy chỉnh — UserName của Identity được set = email.
/// </summary>
public sealed class ApplicationUser : IdentityUser<string>
{
    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    /// <summary>D11 — Admin có thể deactivate (ban). false → 403 AUTH_ACCOUNT_DISABLED.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public ApplicationUser() => Id = Guid.CreateVersion7().ToString();
}
