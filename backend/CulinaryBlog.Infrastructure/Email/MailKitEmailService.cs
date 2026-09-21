using CulinaryBlog.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CulinaryBlog.Infrastructure.Email;

/// <summary>
/// Hiện thực tối giản cho FR-AUTH-001 (bước 11 — enqueue welcome email). Nội dung email
/// (HTML template) và retry policy tinh chỉnh (1p/5p/30p) thuộc FR-JOB-001 — chưa làm ở đây.
/// </summary>
public sealed class MailKitEmailService(IConfiguration configuration, ILogger<MailKitEmailService> logger)
    : IEmailService
{
    public async Task SendWelcomeEmailAsync(string toEmail, string displayName, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            configuration["Smtp:FromName"] ?? "Culinary Blog",
            configuration["Smtp:FromAddress"] ?? "no-reply@culinaryblog.local"));
        message.To.Add(new MailboxAddress(displayName, toEmail));
        message.Subject = "Chào mừng bạn đến với Culinary Blog!";
        message.Body = new TextPart("plain")
        {
            Text = $"Chào {displayName},\n\nCảm ơn bạn đã đăng ký tài khoản Culinary Blog. Chúc bạn có những trải nghiệm nấu ăn thật vui!",
        };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                configuration["Smtp:Host"] ?? "localhost",
                configuration.GetValue("Smtp:Port", 25),
                MailKit.Security.SecureSocketOptions.Auto,
                ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            // NFR-REL-002-style: lỗi gửi mail không được làm sập job. Hangfire tự retry.
            logger.LogWarning(ex, "Gửi welcome email tới {Email} thất bại", toEmail);
            throw;
        }
    }
}
