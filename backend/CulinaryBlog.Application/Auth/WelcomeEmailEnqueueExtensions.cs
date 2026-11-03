using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Auth;

/// <summary>FR-JOB-001/D55 — dùng chung cho Register (FR-AUTH-001) và Google login (FR-AUTH-003).</summary>
internal static class WelcomeEmailEnqueueExtensions
{
    /// <summary>
    /// Enqueue welcome email; lỗi (vd. Hangfire storage không khả dụng) chỉ log Warning — tài khoản đã
    /// tạo, token đã cấp, nên request vẫn thành công. Log <c>UserId</c>, không log email (CONS-010).
    /// </summary>
    public static void TryEnqueueWelcomeEmail(
        this IBackgroundJobService backgroundJobService, string userId, string email, string displayName, ILogger logger)
    {
        try
        {
            backgroundJobService.EnqueueWelcomeEmail(email, displayName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không enqueue được welcome email cho user {UserId} — bỏ qua (D55)", userId);
        }
    }
}
