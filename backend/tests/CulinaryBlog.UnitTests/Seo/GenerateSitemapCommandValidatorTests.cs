using CulinaryBlog.Application.Seo.Commands.GenerateSitemap;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Seo;

/// <summary>FR-JOB-003/D52 — CONS-008: command nội bộ vẫn validate qua pipeline.</summary>
public class GenerateSitemapCommandValidatorTests
{
    private readonly GenerateSitemapCommandValidator _validator = new();

    [Theory(DisplayName = "FR-JOB-003/D52: URL tuyệt đối http/https → pass")]
    [InlineData("https://culinaryblog.vn")]
    [InlineData("http://localhost:3000")]
    [InlineData("https://culinaryblog.vn/")]
    public void AbsoluteHttpUrl_Passes(string url) =>
        _validator.Validate(new GenerateSitemapCommand(url)).IsValid.Should().BeTrue();

    [Theory(DisplayName = "FR-JOB-003/D52: rỗng / tương đối / không phải http(s) → fail")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("culinaryblog.vn")]
    [InlineData("/relative")]
    [InlineData("ftp://culinaryblog.vn")]
    public void InvalidUrl_Fails(string url) =>
        _validator.Validate(new GenerateSitemapCommand(url)).IsValid.Should().BeFalse();
}
