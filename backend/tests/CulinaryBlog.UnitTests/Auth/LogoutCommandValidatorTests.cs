using CulinaryBlog.Application.Auth.Commands.Logout;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-005 — chỉ kiểm tra refreshToken không rỗng và không quá dài (D4, D47).</summary>
public class LogoutCommandValidatorTests
{
    private readonly LogoutCommandValidator _validator = new();

    [Fact(DisplayName = "FR-AUTH-005: refresh token hợp lệ không có lỗi validation")]
    public void Valid_HasNoErrors()
    {
        var result = _validator.TestValidate(new LogoutCommand("q83vEjyCnD2kGf4sTzPq1w=="));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory(DisplayName = "FR-AUTH-005/D4: refresh token rỗng hoặc khoảng trắng → lỗi RefreshToken")]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_HasError(string refreshToken)
    {
        var result = _validator.TestValidate(new LogoutCommand(refreshToken));

        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }

    [Fact(DisplayName = "FR-AUTH-005/D4: refresh token dài quá giới hạn → lỗi RefreshToken")]
    public void TooLong_HasError()
    {
        var tooLong = new string('a', LogoutCommandValidator.MaxTokenLength + 1);

        var result = _validator.TestValidate(new LogoutCommand(tooLong));

        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }
}
