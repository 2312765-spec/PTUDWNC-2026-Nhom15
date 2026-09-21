using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Auth;

/// <summary>
/// FR-AUTH-001 — SRS Chương 3 (đăng ký) + docs/decisions.md D4, D5, D20, D24, D25.
/// Dùng PostgresApiFactory (Testcontainers) vì register ghi thật vào AspNetUsers/RefreshTokens.
/// </summary>
public sealed class RegisterTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-AUTH-001/D24: đăng ký hợp lệ → 201 kèm AuthResponseDto đầy đủ token (auto-login)")]
    public async Task Register_Valid_Returns201WithFullAuthResponse()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var request = new { email, password = "Str0ng!Pass1", displayName = "Nguyễn Văn A" };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        body.User.Email.Should().Be(email);
        body.User.DisplayName.Should().Be("Nguyễn Văn A");
        body.User.Roles.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact(DisplayName = "FR-AUTH-001/D4: email đã tồn tại → 409 AUTH_EMAIL_EXISTS")]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var request = new { email, password = "Str0ng!Pass1", displayName = "Người dùng" };
        (await _client.PostAsJsonAsync("/api/v1/auth/register", request)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthEmailExists);
    }

    [Theory(DisplayName = "D4: dữ liệu không hợp lệ → 400 VALIDATION_ERROR (không phải 422 như SRS Chương 3 ghi)")]
    [InlineData("not-an-email", "Str0ng!Pass1", "Nguyễn Văn A")]
    [InlineData("weak-password@example.com", "weak", "Nguyễn Văn A")]
    [InlineData("no-displayname@example.com", "Str0ng!Pass1", "")]
    public async Task Register_InvalidInput_Returns400VALIDATION_ERROR(string email, string password, string displayName)
    {
        var request = new { email, password, displayName };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }
}
