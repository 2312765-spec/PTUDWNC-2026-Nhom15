using CulinaryBlog.Application.Common.Email;
using CulinaryBlog.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace CulinaryBlog.Infrastructure.Email;

/// <summary>
/// FR-JOB-001/D57 — gửi <see cref="EmailMessage"/> qua SMTP (dev: Mailhog). Thư multipart/alternative
/// (HTML + text). TLS <c>Auto</c>: STARTTLS khi server hỗ trợ. Có <c>Smtp:Username</c> thì đăng nhập.
/// Không bắt lỗi: ném ra để Hangfire retry (D42), LoggingBehavior log Error.
/// </summary>
public sealed class MailKitEmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var host = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Thiếu cấu hình Smtp:Host.");
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            configuration["Smtp:FromName"] ?? "Culinary Blog",
            configuration["Smtp:FromAddress"] ?? "no-reply@culinaryblog.local"));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, configuration.GetValue("Smtp:Port", 25), SecureSocketOptions.Auto, ct);

        var username = configuration["Smtp:Username"];
        if (!string.IsNullOrEmpty(username))
        {
            await client.AuthenticateAsync(username, configuration["Smtp:Password"] ?? string.Empty, ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
