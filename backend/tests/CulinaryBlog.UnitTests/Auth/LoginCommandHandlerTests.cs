using CulinaryBlog.Application.Auth.Commands.Login;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-002 — luồng chính qua mock. Các nhánh lỗi (401/423/403) do IIdentityService ném, đã test riêng ở Infrastructure.</summary>
public class LoginCommandHandlerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private LoginCommandHandler CreateHandler() => new(
        _identityService,
        _jwtService,
        _refreshTokenRepository,
        _unitOfWork);

    [Fact(DisplayName = "FR-AUTH-002/D24: đăng nhập thành công trả AuthResponseDto đầy đủ token, giống shape register")]
    public async Task Handle_Success_ReturnsFullAuthResponse()
    {
        var user = new AuthenticatedUser("user-1", "user@example.com", "Nguyễn Văn A", null, null, ["Author"]);
        var accessExpiresAt = DateTime.UtcNow.AddMinutes(15);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(7);

        _identityService
            .ValidateCredentialsAsync("user@example.com", "correct-password", Arg.Any<CancellationToken>())
            .Returns(user);
        _jwtService
            .GenerateAccessToken("user-1", "user@example.com", user.Roles)
            .Returns(("access-token", accessExpiresAt));
        _jwtService
            .GenerateRefreshToken()
            .Returns(("raw-refresh-token", "hashed-refresh-token", refreshExpiresAt));

        var command = new LoginCommand("user@example.com", "correct-password", "127.0.0.1");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh-token");
        result.ExpiresAt.Should().Be(accessExpiresAt);
        result.User.Email.Should().Be("user@example.com");
        result.User.Roles.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact(DisplayName = "FR-AUTH-002/D20: mỗi lần đăng nhập lưu một refresh token mới (hash, không phải raw)")]
    public async Task Handle_Success_PersistsNewHashedRefreshToken()
    {
        var user = new AuthenticatedUser("user-1", "user@example.com", "A", null, null, ["Author"]);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(7);

        _identityService
            .ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _jwtService
            .GenerateAccessToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
        _jwtService
            .GenerateRefreshToken()
            .Returns(("raw-refresh-token", "hashed-refresh-token", refreshExpiresAt));

        var command = new LoginCommand("user@example.com", "correct-password", "127.0.0.1");

        await CreateHandler().Handle(command, CancellationToken.None);

        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t =>
                t.UserId == "user-1" &&
                t.TokenHash == "hashed-refresh-token" &&
                t.CreatedByIp == "127.0.0.1"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-002/A1: sai credentials → exception từ IIdentityService lan thẳng lên (handler không nuốt lỗi)")]
    public async Task Handle_InvalidCredentials_PropagatesException()
    {
        _identityService
            .ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<AuthenticatedUser>>(_ => throw new UnauthorizedException(
                ErrorCodes.AuthInvalidCredentials,
                "Email hoặc mật khẩu không đúng."));

        var command = new LoginCommand("user@example.com", "wrong-password", null);

        var act = async () => await CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        await _refreshTokenRepository.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }
}
