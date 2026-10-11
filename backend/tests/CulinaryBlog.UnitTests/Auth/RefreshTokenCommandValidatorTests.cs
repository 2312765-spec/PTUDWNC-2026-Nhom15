using CulinaryBlog.Application.Auth.Commands.Refresh;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-004 — chỉ kiểm tra refreshToken không rỗng và không quá dài (D4).</summary>
public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact(DisplayName = "FR-AUTH-004: refresh token hợp lệ không có lỗi validation")]
    public void Valid_HasNoErrors()
    {
        var result = _validator.TestValidate(new RefreshTokenCommand("q83vEjyCnD2kGf4sTzPq1w==", "127.0.0.1"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory(DisplayName = "FR-AUTH-004/D4: refresh token rỗng hoặc khoảng trắng → lỗi RefreshToken")]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_HasError(string refreshToken)
    {
        var result = _validator.TestValidate(new RefreshTokenCommand(refreshToken, null));

        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }

    [Fact(DisplayName = "FR-AUTH-004/D4: refresh token dài quá giới hạn → lỗi RefreshToken")]
    public void TooLong_HasError()
    {
        var tooLong = new string('a', RefreshTokenCommandValidator.MaxTokenLength + 1);

        var result = _validator.TestValidate(new RefreshTokenCommand(tooLong, null));

        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }
}
