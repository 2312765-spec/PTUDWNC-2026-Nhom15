using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Security;

/// <summary>
/// NFR-SEC-003 + docs/decisions.md D15 — rate limit theo IP: /auth/* · API chung · upload ảnh.
/// Mỗi test dựng factory riêng vì bộ đếm nằm trong tiến trình (dùng chung sẽ làm các test dẫm nhau).
/// Route thử cố tình không chạm DB: /auth/login body rỗng → 400, /auth/refresh body rỗng → 400,
/// route không tồn tại → 404, upload body rỗng → 4xx từ endpoint (không bao giờ 429 trước ngưỡng).
/// </summary>
public sealed class RateLimitingTests
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private static readonly Guid RecipeId = Guid.NewGuid();
    private static string UploadUrl => $"/api/v1/recipes/{RecipeId}/images";

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { });

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { });

    private static Task<HttpResponseMessage> GeneralAsync(HttpClient client) =>
        client.GetAsync("/api/v1/khong-ton-tai");

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client) =>
        client.PostAsync(UploadUrl, new StringContent(string.Empty));

    [Fact(DisplayName = "NFR-SEC-003/D15: /auth/* vượt 10 req/phút/IP → 429 RFC 7807 RATE_LIMIT_EXCEEDED + Retry-After")]
    public async Task Auth_ExceedingLimit_Returns429ProblemDetails()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < RateLimitApiFactory.AuthLimit; i++)
        {
            var ok = await LoginAsync(client);
            ok.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body rỗng bị ValidationBehavior từ chối, chưa chạm giới hạn");
        }

        var response = await LoginAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(ErrorCodes.RateLimitExceeded);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(429);

        response.Headers.TryGetValues("Retry-After", out var retryAfter).Should().BeTrue();
        int.Parse(retryAfter!.Single()).Should().BeInRange(1, RateLimitApiFactory.WindowSeconds);
    }

    [Fact(DisplayName = "NFR-SEC-003/D15: 429 kèm X-RateLimit-Limit/Remaining/Reset")]
    public async Task Rejected_Response_CarriesRateLimitHeaders()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();
        for (var i = 0; i < RateLimitApiFactory.AuthLimit; i++) await LoginAsync(client);

        var response = await LoginAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("X-RateLimit-Limit").Single().Should().Be(RateLimitApiFactory.AuthLimit.ToString());
        response.Headers.GetValues("X-RateLimit-Remaining").Single().Should().Be("0");
        long.Parse(response.Headers.GetValues("X-RateLimit-Reset").Single())
            .Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1);
    }

    [Fact(DisplayName = "NFR-SEC-003/D15: response thành công cũng kèm X-RateLimit-*, Remaining giảm dần")]
    public async Task Allowed_Response_CarriesDecreasingRemaining()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        var first = await LoginAsync(client);
        var second = await LoginAsync(client);

        first.Headers.GetValues("X-RateLimit-Limit").Single().Should().Be(RateLimitApiFactory.AuthLimit.ToString());
        first.Headers.GetValues("X-RateLimit-Remaining").Single().Should().Be((RateLimitApiFactory.AuthLimit - 1).ToString());
        second.Headers.GetValues("X-RateLimit-Remaining").Single().Should().Be((RateLimitApiFactory.AuthLimit - 2).ToString());
        first.Headers.Contains("X-RateLimit-Reset").Should().BeTrue();
    }

    [Fact(DisplayName = "NFR-SEC-003/D15: mọi endpoint /auth/* dùng chung một bộ đếm theo IP")]
    public async Task AuthEndpoints_ShareOneCounter()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        await LoginAsync(client);
        await RefreshAsync(client);
        await LoginAsync(client);

        var response = await RefreshAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact(DisplayName = "NFR-SEC-003/D63: đếm theo X-Forwarded-For — Next.js chuyển IP người dùng, mỗi IP một bộ đếm")]
    public async Task ForwardedFor_EachClientIpHasOwnCounter()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        Task<HttpResponseMessage> LoginFromAsync(string ip)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { }) };
            request.Headers.Add("X-Forwarded-For", ip);
            return client.SendAsync(request);
        }

        for (var i = 0; i < RateLimitApiFactory.AuthLimit; i++) await LoginFromAsync("203.0.113.1");

        (await LoginFromAsync("203.0.113.1")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await LoginFromAsync("203.0.113.2")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "NFR-SEC-003/D15: hết lượt /auth/* không chặn API chung (hai bộ đếm riêng)")]
    public async Task AuthExhausted_DoesNotBlockGeneralApi()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();
        for (var i = 0; i <= RateLimitApiFactory.AuthLimit; i++) await LoginAsync(client);

        var response = await GeneralAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "NFR-SEC-003/D15: API chung vượt 100 req/phút/IP → 429")]
    public async Task General_ExceedingLimit_Returns429()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < RateLimitApiFactory.GeneralLimit; i++)
        {
            (await GeneralAsync(client)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var response = await GeneralAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("X-RateLimit-Limit").Single().Should().Be(RateLimitApiFactory.GeneralLimit.ToString());
    }

    [Fact(DisplayName = "NFR-SEC-003/D15: upload ảnh vượt 5 req/phút/IP → 429")]
    public async Task Upload_ExceedingLimit_Returns429()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < RateLimitApiFactory.UploadLimit; i++)
        {
            (await UploadAsync(client)).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        var response = await UploadAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("X-RateLimit-Limit").Single().Should().Be(RateLimitApiFactory.UploadLimit.ToString());
    }

    [Fact(DisplayName = "NFR-SEC-003/D15: chỉ POST .../images bị tính là upload — GET cùng đường dẫn dùng bộ đếm API chung")]
    public async Task GetOnImagesPath_IsGeneralNotUpload()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();
        for (var i = 0; i <= RateLimitApiFactory.UploadLimit; i++) await UploadAsync(client);

        var response = await client.GetAsync(UploadUrl);

        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("X-RateLimit-Limit").Single().Should().Be(RateLimitApiFactory.GeneralLimit.ToString());
    }

    [Fact(DisplayName = "NFR-SEC-003/NFR-REL: /health không bị rate limit (orchestrator gọi liên tục)")]
    public async Task Health_IsNeverRateLimited()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < RateLimitApiFactory.GeneralLimit * 3; i++)
        {
            var response = await client.GetAsync("/health");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact(DisplayName = "NFR-SEC-003/CONS-010: 429 có correlationId trong ProblemDetails")]
    public async Task Rejected_Response_HasCorrelationId()
    {
        await using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();
        for (var i = 0; i < RateLimitApiFactory.AuthLimit; i++) await LoginAsync(client);

        var response = await LoginAsync(client);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("correlationId", out var id).Should().BeTrue();
        id.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "NFR-SEC-003: môi trường Testing mặc định TẮT rate limit (không ảnh hưởng test khác)")]
    public async Task Disabled_ByDefault_InTesting()
    {
        await using var factory = new CulinaryBlogApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            (await LoginAsync(client)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
