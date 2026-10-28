using CulinaryBlog.API.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// NFR-SEC-007, CONS-004 — API không được khởi động với khóa ký JWT rỗng/yếu. Trước đây Program.cs
/// tự rơi về một khóa hardcode trong mã nguồn khi thiếu <c>Jwt:Key</c>, tức prod quên cấu hình vẫn chạy
/// với khóa ai cũng biết. Nằm ở IntegrationTests vì UnitTests chỉ tham chiếu tầng Application.
/// </summary>
public sealed class JwtKeyGuardTests
{
    [Theory(DisplayName = "NFR-SEC-007: Jwt:Key thiếu / rỗng / < 32 byte → ném lỗi khi khởi động")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("khoa-ngan-qua-chi-31-ky-tu-abcd")]
    public void GetSigningKey_MissingOrShortKey_Throws(string? key)
    {
        var configuration = Configuration(key);

        var act = () => JwtKeyGuard.GetSigningKey(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:Key*");
    }

    [Fact(DisplayName = "NFR-SEC-007: Jwt:Key ≥ 32 byte (HS256) → trả đúng khóa")]
    public void GetSigningKey_ValidKey_ReturnsKey()
    {
        const string key = "khoa-hop-le-dai-it-nhat-32-ky-tu-cho-hs256"; // gitleaks:allow — khóa giả cho test, không phải secret

        JwtKeyGuard.GetSigningKey(Configuration(key)).Should().Be(key);
    }

    private static IConfiguration Configuration(string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key })
            .Build();
}
