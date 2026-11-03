using CulinaryBlog.Application.Auth.Commands.GoogleLogin;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-003 + FR-JOB-001/D56 — displayName khi Google không trả claim <c>name</c>.</summary>
public class GoogleLoginCommandHandlerTests
{
    private readonly IGoogleTokenValidator _googleTokenValidator = Substitute.For<IGoogleTokenValidator>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IBackgroundJobService _backgroundJobService = Substitute.For<IBackgroundJobService>();

    public GoogleLoginCommandHandlerTests()
    {
        _jwtService
            .GenerateAccessToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
        _jwtService
            .GenerateRefreshToken()
            .Returns(("raw-refresh-token", "hashed-refresh-token", DateTime.UtcNow.AddDays(7)));
        _identityService
            .LoginOrRegisterWithGoogleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => (
                new AuthenticatedUser("user-1", call.ArgAt<string>(0), call.ArgAt<string>(1), null, null, ["Author"]),
                true));
    }

    private GoogleLoginCommandHandler CreateHandler() => new(
        _googleTokenValidator,
        _identityService,
        _jwtService,
        _refreshTokenRepository,
        _unitOfWork,
        _backgroundJobService,
        NullLogger<GoogleLoginCommandHandler>.Instance);

    private void GoogleReturns(string? name) =>
        _googleTokenValidator
            .ValidateAsync("id-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserInfo("lan.nguyen@gmail.com", name!, null, "google-sub-1", EmailVerified: true));

    [Theory(DisplayName = "FR-AUTH-003/D56: Google không trả name → displayName là phần trước @ của email, welcome email dùng tên đó")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_GoogleNameMissing_FallsBackToEmailLocalPart(string? name)
    {
        GoogleReturns(name);

        var result = await CreateHandler().Handle(new GoogleLoginCommand("id-token", null), CancellationToken.None);

        await _identityService.Received(1).LoginOrRegisterWithGoogleAsync(
            "lan.nguyen@gmail.com", "lan.nguyen", null, "google-sub-1", Arg.Any<CancellationToken>());
        result.User.DisplayName.Should().Be("lan.nguyen");
        _backgroundJobService.Received(1).EnqueueWelcomeEmail("lan.nguyen@gmail.com", "lan.nguyen");
    }

    [Fact(DisplayName = "FR-AUTH-003: Google có name → dùng name (đã trim) làm displayName")]
    public async Task Handle_GoogleNamePresent_UsesTrimmedName()
    {
        GoogleReturns("  Nguyễn Thị Lan ");

        await CreateHandler().Handle(new GoogleLoginCommand("id-token", null), CancellationToken.None);

        await _identityService.Received(1).LoginOrRegisterWithGoogleAsync(
            "lan.nguyen@gmail.com", "Nguyễn Thị Lan", null, "google-sub-1", Arg.Any<CancellationToken>());
    }
}
