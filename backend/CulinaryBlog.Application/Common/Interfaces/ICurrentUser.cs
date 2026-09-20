namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// HỢP ĐỒNG CHUNG — chủ sở hữu: A. Chốt tuần 1, đóng băng sau đó.
/// B, C, D dùng để biết ai đang gọi request. Implement ở API layer (HttpContext).
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool IsInRole(string role);
}
