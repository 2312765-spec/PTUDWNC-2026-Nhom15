using CulinaryBlog.Application.Auth.Commands.Login;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-002 — chỉ kiểm tra định dạng email + password không rỗng (không áp lại rule độ phức tạp).</summary>
public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact(DisplayName = "FR-AUTH-002: dữ liệu hợp lệ không có lỗi validation")]
    public void Valid_HasNoErrors()
    {
        var result = _validator.TestValidate(new LoginCommand("user@example.com", "any-password", "127.0.0.1"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory(DisplayName = "FR-AUTH-002/D4: email rỗng hoặc sai định dạng → lỗi Email")]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void InvalidEmail_HasError(string email)
    {
        var result = _validator.TestValidate(new LoginCommand(email, "any-password", null));

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact(DisplayName = "FR-AUTH-002/D4: password rỗng → lỗi Password (không kiểm tra độ phức tạp khi login)")]
    public void EmptyPassword_HasError()
    {
        var result = _validator.TestValidate(new LoginCommand("user@example.com", "", null));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact(DisplayName = "FR-AUTH-002: password yếu (không hoa/số/ký tự đặc biệt) vẫn hợp lệ ở login")]
    public void WeakPassword_NoComplexityError()
    {
        var result = _validator.TestValidate(new LoginCommand("user@example.com", "weak", null));

        result.ShouldNotHaveValidationErrorFor(c => c.Password);
    }
}
