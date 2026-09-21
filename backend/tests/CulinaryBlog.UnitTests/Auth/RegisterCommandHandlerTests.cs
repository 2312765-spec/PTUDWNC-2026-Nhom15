using CulinaryBlog.Application.Auth.Commands.Register;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-001 — luồng chính (SRS Chương 3 bước 4-12) qua mock, không cần DB thật.</summary>
public class RegisterCommandHandlerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IBackgroundJobService _backgroundJobService = Substitute.For<IBackgroundJobService>();

    private RegisterCommandHandler CreateHandler() => new(
        _identityService,
        _jwtService,
        _refreshTokenRepository,
        _unitOfWork,
        _backgroundJobService);

    [Fact(DisplayName = "FR-AUTH-001: đăng ký thành công trả AuthResponseDto đầy đủ token (D24), auto-login")]
    public async Task Handle_Success_ReturnsFullAuthResponse()
    {
        var createdUser = new CreatedUser("user-1", "user@example.com", "Nguyễn Văn A", null, null, ["Author"]);
        var accessExpiresAt = DateTime.UtcNow.AddMinutes(15);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(7);

        _identityService
            .CreateUserAsync("user@example.com", "Str0ng!Pass", "Nguyễn Văn A", Arg.Any<CancellationToken>())
            .Returns(createdUser);
        _jwtService
            .GenerateAccessToken("user-1", "user@example.com", createdUser.Roles)
            .Returns(("access-token", accessExpiresAt));
        _jwtService
            .GenerateRefreshToken()
            .Returns(("raw-refresh-token", "hashed-refresh-token", refreshExpiresAt));

        var command = new RegisterCommand("user@example.com", "Str0ng!Pass", "Nguyễn Văn A", "127.0.0.1");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh-token");
        result.ExpiresAt.Should().Be(accessExpiresAt);
        result.User.Id.Should().Be("user-1");
        result.User.Email.Should().Be("user@example.com");
        result.User.DisplayName.Should().Be("Nguyễn Văn A");
        result.User.Roles.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact(DisplayName = "FR-AUTH-001/D20: refresh token được lưu với TokenHash (không phải raw) và SaveChanges được gọi")]
    public async Task Handle_Success_PersistsHashedRefreshToken()
    {
        var createdUser = new CreatedUser("user-1", "user@example.com", "A", null, null, ["Author"]);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(7);

        _identityService
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(createdUser);
        _jwtService
            .GenerateAccessToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
        _jwtService
            .GenerateRefreshToken()
            .Returns(("raw-refresh-token", "hashed-refresh-token", refreshExpiresAt));

        var command = new RegisterCommand("user@example.com", "Str0ng!Pass", "A", "127.0.0.1");

        await CreateHandler().Handle(command, CancellationToken.None);

        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t =>
                t.UserId == "user-1" &&
                t.TokenHash == "hashed-refresh-token" &&
                t.CreatedByIp == "127.0.0.1"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-001 bước 11: enqueue welcome email fire-and-forget sau khi đăng ký thành công")]
    public async Task Handle_Success_EnqueuesWelcomeEmail()
    {
        var createdUser = new CreatedUser("user-1", "user@example.com", "Nguyễn Văn A", null, null, ["Author"]);

        _identityService
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(createdUser);
        _jwtService
            .GenerateAccessToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
        _jwtService
            .GenerateRefreshToken()
            .Returns(("raw-refresh-token", "hashed-refresh-token", DateTime.UtcNow.AddDays(7)));

        var command = new RegisterCommand("user@example.com", "Str0ng!Pass", "Nguyễn Văn A", null);

        await CreateHandler().Handle(command, CancellationToken.None);

        _backgroundJobService.Received(1).EnqueueWelcomeEmail("user@example.com", "Nguyễn Văn A");
    }
}
