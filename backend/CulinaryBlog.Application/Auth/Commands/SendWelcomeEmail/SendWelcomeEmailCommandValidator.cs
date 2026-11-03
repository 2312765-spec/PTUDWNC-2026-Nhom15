using FluentValidation;

namespace CulinaryBlog.Application.Auth.Commands.SendWelcomeEmail;

/// <summary>
/// FR-JOB-001/D57 — thiếu hoặc sai Seo:SiteBaseUrl thì job lỗi (Hangfire retry rồi Failed), không gửi
/// email có link hỏng.
/// </summary>
public sealed class SendWelcomeEmailCommandValidator : AbstractValidator<SendWelcomeEmailCommand>
{
    public SendWelcomeEmailCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();

        RuleFor(x => x.DisplayName)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("DisplayName không được rỗng.");

        RuleFor(x => x.SiteBaseUrl)
            .NotEmpty()
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("Seo:SiteBaseUrl phải là URL tuyệt đối http(s).");
    }

    private static bool BeAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
