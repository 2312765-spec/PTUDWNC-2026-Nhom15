using CulinaryBlog.Application.Auth.Email;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.SendWelcomeEmail;

/// <summary>
/// FR-JOB-001 — dựng email chào mừng rồi gửi. Lỗi SMTP không bắt ở đây: LoggingBehavior log Error,
/// Hangfire retry theo D42, hết lượt thì job Failed.
/// </summary>
public sealed class SendWelcomeEmailCommandHandler(IEmailService emailService)
    : IRequestHandler<SendWelcomeEmailCommand>
{
    public Task Handle(SendWelcomeEmailCommand request, CancellationToken cancellationToken) =>
        emailService.SendAsync(
            WelcomeEmailTemplate.Build(request.Email, request.DisplayName, request.SiteBaseUrl),
            cancellationToken);
}
