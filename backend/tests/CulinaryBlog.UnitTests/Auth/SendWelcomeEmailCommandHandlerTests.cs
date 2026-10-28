using CulinaryBlog.Application.Auth.Commands.SendWelcomeEmail;
using CulinaryBlog.Application.Auth.Email;
using CulinaryBlog.Application.Common.Email;
using CulinaryBlog.Application.Common.Interfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-JOB-001 — handler dựng email từ template rồi gửi qua IEmailService.</summary>
public class SendWelcomeEmailCommandHandlerTests
{
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();

    [Fact(DisplayName = "FR-JOB-001: gửi đúng 1 email, nội dung đúng như WelcomeEmailTemplate dựng")]
    public async Task Handle_SendsTemplateMessageOnce()
    {
        var handler = new SendWelcomeEmailCommandHandler(_emailService);
        var expected = WelcomeEmailTemplate.Build("a@example.com", "Tên A", "https://culinary.test");

        await handler.Handle(new SendWelcomeEmailCommand("a@example.com", "Tên A", "https://culinary.test"), CancellationToken.None);

        await _emailService.Received(1).SendAsync(expected, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-JOB-001/D42: IEmailService lỗi → handler ném lại (để Hangfire retry), không nuốt lỗi")]
    public async Task Handle_EmailServiceThrows_Propagates()
    {
        _emailService.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("SMTP down"));
        var handler = new SendWelcomeEmailCommandHandler(_emailService);

        var act = () => handler.Handle(
            new SendWelcomeEmailCommand("a@example.com", "Tên A", "https://culinary.test"), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
    }
}
