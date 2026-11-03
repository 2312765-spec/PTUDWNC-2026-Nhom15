using System.Net;
using CulinaryBlog.Application.Common.Email;

namespace CulinaryBlog.Application.Auth.Email;

/// <summary>
/// FR-JOB-001 — nội dung email chào mừng. D12: không có link kích hoạt, chỉ link trang chủ và
/// <c>/dashboard</c>. D57: <c>displayName</c> do người dùng nhập nên luôn HTML-encode.
/// </summary>
public static class WelcomeEmailTemplate
{
    public const string Subject = "Chào mừng bạn đến với Culinary Blog!";

    public static EmailMessage Build(string toAddress, string displayName, string siteBaseUrl)
    {
        var baseUrl = siteBaseUrl.TrimEnd('/');
        var homeUrl = $"{baseUrl}/";
        var dashboardUrl = $"{baseUrl}/dashboard";

        var name = WebUtility.HtmlEncode(displayName);
        var homeHref = WebUtility.HtmlEncode(homeUrl);
        var dashboardHref = WebUtility.HtmlEncode(dashboardUrl);

        var html = $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head><meta charset="utf-8"><title>{Subject}</title></head>
            <body style="margin:0;padding:24px;background:#f6f3ee;font-family:Arial,Helvetica,sans-serif;color:#2d2a26;">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:32px;">
                <h1 style="margin:0 0 16px;font-size:22px;color:#b4532a;">Chào {name}!</h1>
                <p style="margin:0 0 12px;line-height:1.6;">Cảm ơn bạn đã tham gia Culinary Blog. Từ giờ bạn có thể viết, lưu nháp và chia sẻ công thức nấu ăn của riêng mình.</p>
                <p style="margin:24px 0;">
                  <a href="{dashboardHref}" style="display:inline-block;padding:12px 20px;background:#b4532a;color:#ffffff;text-decoration:none;border-radius:6px;">Viết công thức đầu tiên</a>
                </p>
                <p style="margin:0;line-height:1.6;">Hoặc ghé <a href="{homeHref}" style="color:#b4532a;">trang chủ Culinary Blog</a> để tìm cảm hứng.</p>
              </div>
            </body>
            </html>
            """;

        var text = $"""
            Chào {displayName}!

            Cảm ơn bạn đã tham gia Culinary Blog. Từ giờ bạn có thể viết, lưu nháp và chia sẻ công thức nấu ăn của riêng mình.

            Viết công thức đầu tiên: {dashboardUrl}
            Trang chủ: {homeUrl}
            """;

        return new EmailMessage(toAddress, displayName, Subject, html, text);
    }
}
