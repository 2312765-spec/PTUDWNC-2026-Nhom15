using FluentValidation;

namespace CulinaryBlog.Application.Auth.Commands.Refresh;

/// <summary>
/// CONS-008 — chạy trong ValidationBehavior. Rỗng/khoảng trắng → 400 VALIDATION_ERROR (D4).
/// Giới hạn độ dài chặn payload rác trước khi hash + tra DB (RT hợp lệ chỉ ~24 ký tự, D25).
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public const int MaxTokenLength = 256;

    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(MaxTokenLength);
    }
}
