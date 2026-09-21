using FluentValidation;

namespace CulinaryBlog.Application.Auth.Commands.Login;

/// <summary>
/// CONS-008 — chạy trong ValidationBehavior. SRS Chương 3 bước 3: chỉ kiểm tra định dạng
/// email và password không rỗng — KHÔNG áp lại rule độ phức tạp password (đó là của đăng ký).
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
