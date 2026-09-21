using FluentValidation;

namespace CulinaryBlog.Application.Auth.Commands.Register;

/// <summary>
/// CONS-008 — validate CHỈ ở đây, chạy trong ValidationBehavior. Ràng buộc theo
/// docs/decisions.md D5 (displayName) và mục "Ràng buộc validator" (password, NFR-SEC-001).
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .Length(2, 100);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Password phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Password phải có ít nhất 1 chữ số.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password phải có ít nhất 1 ký tự đặc biệt.");
    }
}
