using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Security;

/// <summary>
/// NFR-SEC-004/D68 — XSS: mọi response của API mang Content-Security-Policy chặt (API chỉ trả JSON,
/// không có lý do tải script/ảnh/khung). Đúng MỘT header — Nginx không thêm (tránh hai policy chồng nhau).
/// </summary>
public sealed class CspTests(CulinaryBlogApiFactory factory) : IClassFixture<CulinaryBlogApiFactory>
{
    private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'";

    private readonly HttpClient _client = factory.CreateClient();

    [Theory(DisplayName = "NFR-SEC-004/D68: response API có đúng một CSP default-src 'none'")]
    [InlineData("/health/live")]
    [InlineData("/api/v1/recipes/khong-ton-tai-slug")]   // cả response lỗi (RFC 7807)
    [InlineData("/api/v1/khong-co-route")]                // cả 404 của routing
    public async Task ApiResponse_HasStrictCsp(string path)
    {
        var response = await _client.GetAsync(path);

        response.Headers.TryGetValues("Content-Security-Policy", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be(ApiPolicy);
    }
}
