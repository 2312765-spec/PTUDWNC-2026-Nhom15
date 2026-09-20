using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Identity;

/// <summary>
/// SRS 7.7 — kế thừa IdentityUser&lt;string&gt;, bảng "AspNetUsers".
/// Nằm ở Infrastructure (không phải Domain) vì CONS-001 cấm Domain reference bất kỳ
/// package ngoài nào — IdentityUser đến từ Microsoft.AspNetCore.Identity.
/// D5: KHÔNG có FullName/UserName tùy chỉnh — UserName của Identity được set = email.
/// </summary>
public sealed class ApplicationUser : IdentityUser<string>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }

    /// <summary>D11 — false thì đăng nhập/refresh trả 403 AUTH_ACCOUNT_DISABLED.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public ApplicationUser() => Id = Guid.CreateVersion7().ToString();
}
