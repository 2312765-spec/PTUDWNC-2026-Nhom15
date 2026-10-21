using CulinaryBlog.Application.Auth.Email;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-JOB-001 — nội dung email chào mừng (D12, D57).</summary>
public class WelcomeEmailTemplateTests
{
    [Fact(DisplayName = "FR-JOB-001/D12: email có tên, link trang chủ và /dashboard ở cả HTML lẫn text")]
    public void Build_ContainsNameAndLinks()
    {
        var message = WelcomeEmailTemplate.Build("a@example.com", "Nguyễn Văn A", "https://culinary.test");

        message.ToAddress.Should().Be("a@example.com");
        message.ToName.Should().Be("Nguyễn Văn A");
        message.Subject.Should().Be(WelcomeEmailTemplate.Subject);
        message.HtmlBody.Should().Contain("Nguyễn Văn A")
            .And.Contain("href=\"https://culinary.test/\"")
            .And.Contain("href=\"https://culinary.test/dashboard\"");
        message.TextBody.Should().Contain("Nguyễn Văn A")
            .And.Contain("https://culinary.test/")
            .And.Contain("https://culinary.test/dashboard");
    }

    [Fact(DisplayName = "FR-JOB-001/D57: displayName chứa HTML bị encode ở phần HTML")]
    public void Build_EncodesDisplayNameInHtml()
    {
        var message = WelcomeEmailTemplate.Build("a@example.com", "<b onclick=\"x\">A</b>", "https://culinary.test");

        message.HtmlBody.Should().NotContain("<b onclick")
            .And.Contain("&lt;b onclick=&quot;x&quot;&gt;A&lt;/b&gt;");
    }

    [Fact(DisplayName = "FR-JOB-001/D57: base URL có dấu / cuối → không sinh //dashboard")]
    public void Build_TrailingSlashBaseUrl_NoDoubleSlash()
    {
        var message = WelcomeEmailTemplate.Build("a@example.com", "A", "https://culinary.test/");

        message.HtmlBody.Should().Contain("href=\"https://culinary.test/dashboard\"")
            .And.NotContain("test//");
        message.TextBody.Should().NotContain("test//");
    }

    [Fact(DisplayName = "FR-JOB-001/D12: không có link hay lời nhắc kích hoạt / xác nhận email")]
    public void Build_HasNoActivationContent()
    {
        var message = WelcomeEmailTemplate.Build("a@example.com", "A", "https://culinary.test");

        foreach (var body in new[] { message.HtmlBody, message.TextBody })
        {
            body.Should().NotContainEquivalentOf("kích hoạt")
                .And.NotContainEquivalentOf("xác nhận")
                .And.NotContainEquivalentOf("verify")
                .And.NotContainEquivalentOf("confirm");
        }
    }
}
