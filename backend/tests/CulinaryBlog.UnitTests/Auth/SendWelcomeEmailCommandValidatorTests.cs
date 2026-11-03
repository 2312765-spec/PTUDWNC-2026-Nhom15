using CulinaryBlog.Application.Auth.Commands.SendWelcomeEmail;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-JOB-001/D57 — CONS-008: kiểm tra đầu vào job qua FluentValidation.</summary>
public class SendWelcomeEmailCommandValidatorTests
{
    private readonly SendWelcomeEmailCommandValidator _validator = new();

    [Theory(DisplayName = "FR-JOB-001/D57: command hợp lệ (http và https)")]
    [InlineData("https://culinary.test")]
    [InlineData("http://localhost:3000/")]
    public void Valid(string siteBaseUrl)
    {
        _validator.Validate(new SendWelcomeEmailCommand("a@example.com", "Tên A", siteBaseUrl))
            .IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "FR-JOB-001/D57: email sai / displayName rỗng / base URL thiếu, tương đối, không phải http(s) → lỗi")]
    [InlineData("khong-phai-email", "Tên", "https://culinary.test", "Email")]
    [InlineData("", "Tên", "https://culinary.test", "Email")]
    [InlineData("a@example.com", "", "https://culinary.test", "DisplayName")]
    [InlineData("a@example.com", "   ", "https://culinary.test", "DisplayName")]
    [InlineData("a@example.com", "Tên", "", "SiteBaseUrl")]
    [InlineData("a@example.com", "Tên", "culinary.test", "SiteBaseUrl")]
    [InlineData("a@example.com", "Tên", "/relative", "SiteBaseUrl")]
    [InlineData("a@example.com", "Tên", "ftp://culinary.test", "SiteBaseUrl")]
    public void Invalid(string email, string displayName, string siteBaseUrl, string property)
    {
        var result = _validator.Validate(new SendWelcomeEmailCommand(email, displayName, siteBaseUrl));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == property);
    }
}
