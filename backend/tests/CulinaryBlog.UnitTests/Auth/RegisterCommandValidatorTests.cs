using CulinaryBlog.Application.Auth.Commands.Register;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-001 — D5 (displayName), NFR-SEC-001/decisions.md "Ràng buộc validator" (password).</summary>
public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand ValidCommand(
        string email = "user@example.com",
        string password = "Str0ng!Pass",
        string displayName = "Nguyễn Văn A") =>
        new(email, password, displayName, "127.0.0.1");

    [Fact(DisplayName = "FR-AUTH-001: dữ liệu hợp lệ không có lỗi validation")]
    public void Valid_HasNoErrors()
    {
        var result = _validator.TestValidate(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory(DisplayName = "FR-AUTH-001/D4: email rỗng hoặc sai định dạng → lỗi Email")]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void InvalidEmail_HasError(string email)
    {
        var result = _validator.TestValidate(ValidCommand(email: email));

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Theory(DisplayName = "docs/decisions.md — password thiếu 1 trong 4 lớp ký tự → lỗi Password")]
    [InlineData("short1!")] // < 8 ký tự
    [InlineData("nouppercase1!")] // thiếu chữ hoa
    [InlineData("NOLOWERCASE1!")] // thiếu chữ thường
    [InlineData("NoDigitHere!")] // thiếu số
    [InlineData("NoSpecial123")] // thiếu ký tự đặc biệt
    public void WeakPassword_HasError(string password)
    {
        var result = _validator.TestValidate(ValidCommand(password: password));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Theory(DisplayName = "D5: displayName rỗng hoặc ngoài khoảng 2-100 ký tự → lỗi DisplayName")]
    [InlineData("")]
    [InlineData("A")]
    public void InvalidDisplayName_HasError(string displayName)
    {
        var result = _validator.TestValidate(ValidCommand(displayName: displayName));

        result.ShouldHaveValidationErrorFor(c => c.DisplayName);
    }
}
