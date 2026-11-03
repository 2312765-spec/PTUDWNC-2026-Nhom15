using System.Collections.Concurrent;
using CulinaryBlog.Application.Common.Email;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.IntegrationTests.Recipes.Support;

/// <summary>
/// FR-JOB-001 — thay <c>MailKitEmailService</c> để kiểm tra nội dung email mà job dựng ra, không cần
/// SMTP. Việc gửi SMTP thật được kiểm ở <c>Email/MailKitEmailServiceTests</c> (Mailhog).
/// </summary>
public sealed class RecordingEmailService : IEmailService
{
    public ConcurrentBag<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
