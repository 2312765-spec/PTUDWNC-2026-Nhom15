using System.Net;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Security;

/// <summary>
/// NFR-SEC-005 — CORS chỉ cho phép origin cấu hình trong appsettings (<c>Cors:AllowedOrigins</c>),
/// không wildcard "*". Preflight được CORS middleware trả lời trước khi tới routing nên không cần DB.
/// </summary>
public sealed class CorsTests(CulinaryBlogApiFactory factory) : IClassFixture<CulinaryBlogApiFactory>
{
    private const string AllowedOrigin = "http://localhost:3000"; // khớp CulinaryBlogApiFactory

    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> PreflightAsync(string origin, string method = "POST")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return _client.SendAsync(request);
    }

    [Fact(DisplayName = "NFR-SEC-005: origin trong allowlist → preflight được phép, echo đúng origin (không '*')")]
    public async Task AllowedOrigin_Preflight_EchoesOrigin()
    {
        var response = await PreflightAsync(AllowedOrigin);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be(AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Single().Should().Be("true");
    }

    [Theory(DisplayName = "NFR-SEC-005: origin ngoài allowlist → không có Access-Control-Allow-Origin")]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost:3001")]
    [InlineData("http://localhost:3000.evil.example")]
    public async Task UnknownOrigin_Preflight_IsNotAllowed(string origin)
    {
        var response = await PreflightAsync(origin);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact(DisplayName = "NFR-SEC-005: request thường từ origin lạ không nhận header CORS")]
    public async Task UnknownOrigin_SimpleRequest_GetsNoCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/khong-ton-tai");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact(DisplayName = "NFR-SEC-005: không bao giờ trả wildcard '*' cho Access-Control-Allow-Origin")]
    public async Task AllowOrigin_IsNeverWildcard()
    {
        var response = await PreflightAsync(AllowedOrigin, "GET");

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().NotContain("*");
    }
}
