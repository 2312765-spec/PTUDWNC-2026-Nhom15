using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// CONS-009/D57, NFR-SEC-005 — sau Nginx, API chỉ biết request gốc là HTTPS qua <c>X-Forwarded-Proto</c>.
/// Quan sát qua HSTS: <c>UseHsts</c> (môi trường khác Development) chỉ gửi header khi
/// <c>Request.IsHttps</c>. Host phải khác localhost — HSTS mặc định bỏ qua localhost/127.0.0.1.
/// TestServer không có RemoteIpAddress, nên test cũng xác nhận đã xóa danh sách proxy tin cậy mặc định (loopback).
/// </summary>
public sealed class ForwardedHeadersTests(CulinaryBlogApiFactory factory) : IClassFixture<CulinaryBlogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://culinary.example/"),
    });

    [Fact(DisplayName = "CONS-009/D57: X-Forwarded-Proto https → API coi request là HTTPS, gửi HSTS 1 năm")]
    public async Task ForwardedProtoHttps_SendsHsts()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("Strict-Transport-Security", out var values).Should().BeTrue();
        values!.Single().Should().Contain("max-age=31536000");
    }

    [Fact(DisplayName = "CONS-009/D57: không có X-Forwarded-Proto → request HTTP, không gửi HSTS")]
    public async Task NoForwardedProto_NoHsts()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }
}
