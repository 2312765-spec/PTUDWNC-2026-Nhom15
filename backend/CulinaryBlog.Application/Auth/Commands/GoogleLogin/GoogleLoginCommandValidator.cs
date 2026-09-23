using FluentValidation;

namespace CulinaryBlog.Application.Auth.Commands.GoogleLogin;

/// <summary>CONS-008 — validate CHỈ ở đây, chạy trong ValidationBehavior.</summary>
public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty();
    }
}
