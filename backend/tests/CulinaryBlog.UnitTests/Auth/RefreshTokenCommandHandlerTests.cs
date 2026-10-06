using CulinaryBlog.Application.Auth.Commands.Refresh;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.UnitTests.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-004 — mọi nhánh của handler qua mock (D11, D20, D24, D35).</summary>
public class RefreshTokenCommandHandlerTests
{
    private const string RawToken = "raw-old-token";
    private const string OldHash = "hash-old";
    private const string NewHash = "hash-new";
    private const string UserId = "user-1";

    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeLogger<RefreshTokenCommandHandler> _logger = new();

    private readonly AuthenticatedUser _user = new(UserId, "user@example.com", "Nguyễn Văn A", null, null, ["Author"]);
    private readonly DateTime _accessExpiresAt = DateTime.UtcNow.AddMinutes(15);

    public RefreshTokenCommandHandlerTests()
    {
        _jwtService.HashToken(RawToken).Returns(OldHash);
        _jwtService
            .GenerateAccessToken(UserId, "user@example.com", Arg.Any<IEnumerable<string>>())
            .Returns(("access-token", _accessExpiresAt));
        _jwtService
            .GenerateRefreshToken()
            .Returns(("raw-new-token", NewHash, DateTime.UtcNow.AddDays(7)));
        _identityService.GetUserForRefreshAsync(UserId, Arg.Any<CancellationToken>()).Returns(_user);
        _refreshTokenRepository
            .TryRevokeAsync(OldHash, NewHash, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private RefreshTokenCommandHandler CreateHandler() => new(
        _identityService,
        _jwtService,
        _refreshTokenRepository,
        _unitOfWork,
        _logger);

    private static RefreshTokenCommand Command() => new(RawToken, "127.0.0.1");

    private void GivenStoredToken(DateTime expiresAt, bool revoked = false)
    {
        var token = RefreshToken.Create(UserId, OldHash, expiresAt);
        if (revoked)
        {
            token.Revoke(NewHash);
        }

        _refreshTokenRepository.GetByTokenAsync(OldHash, Arg.Any<CancellationToken>()).Returns(token);
    }

    [Fact(DisplayName = "FR-AUTH-004/D24: RT hợp lệ → AuthResponseDto với cặp token mới")]
    public async Task Handle_ValidToken_ReturnsNewTokenPair()
    {
        GivenStoredToken(DateTime.UtcNow.AddDays(1));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-new-token");
        result.ExpiresAt.Should().Be(_accessExpiresAt);
        result.User.Id.Should().Be(UserId);
        result.User.Roles.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact(DisplayName = "FR-AUTH-004/D20: rotation — revoke RT cũ trỏ tới hash RT mới, lưu RT mới (hash, không raw)")]
    public async Task Handle_ValidToken_RotatesAndPersists()
    {
        GivenStoredToken(DateTime.UtcNow.AddDays(1));

        await CreateHandler().Handle(Command(), CancellationToken.None);

        await _refreshTokenRepository.Received(1).TryRevokeAsync(OldHash, NewHash, Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.UserId == UserId && t.TokenHash == NewHash && t.CreatedByIp == "127.0.0.1"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-004/A1,D35: RT không tồn tại → 401 AUTH_TOKEN_INVALID")]
    public async Task Handle_UnknownToken_ThrowsTokenInvalid()
    {
        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AuthTokenInvalid);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-004/A2: RT hết hạn → 401 AUTH_REFRESH_TOKEN_EXPIRED, không revoke gì")]
    public async Task Handle_ExpiredToken_ThrowsExpired()
    {
        GivenStoredToken(DateTime.UtcNow.AddMinutes(-1));

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AuthRefreshTokenExpired);
        await _refreshTokenRepository.DidNotReceive().RevokeAllActiveForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-004/A3,D35: reuse → revoke mọi RT của user + log WARNING + 401 AUTH_REFRESH_TOKEN_REVOKED")]
    public async Task Handle_RevokedToken_RevokesAllAndLogsWarning()
    {
        GivenStoredToken(DateTime.UtcNow.AddDays(1), revoked: true);

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AuthRefreshTokenRevoked);
        await _refreshTokenRepository.Received(1).RevokeAllActiveForUserAsync(UserId, Arg.Any<CancellationToken>());
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning)
            .Which.Message.Should().Contain(UserId).And.NotContain(RawToken).And.NotContain(OldHash);
        _jwtService.DidNotReceive().GenerateRefreshToken();
    }

    [Fact(DisplayName = "FR-AUTH-004/D35: RT vừa revoke vừa hết hạn → vẫn là reuse (REVOKED), kiểm tra revoked trước expired")]
    public async Task Handle_RevokedAndExpiredToken_TreatedAsReuse()
    {
        GivenStoredToken(DateTime.UtcNow.AddMinutes(-1), revoked: true);

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AuthRefreshTokenRevoked);
        await _refreshTokenRepository.Received(1).RevokeAllActiveForUserAsync(UserId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-004/A4,D35: user bị xóa → 401 AUTH_TOKEN_INVALID")]
    public async Task Handle_DeletedUser_ThrowsTokenInvalid()
    {
        GivenStoredToken(DateTime.UtcNow.AddDays(1));
        _identityService.GetUserForRefreshAsync(UserId, Arg.Any<CancellationToken>()).Returns((AuthenticatedUser?)null);

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AuthTokenInvalid);
        await _refreshTokenRepository.DidNotReceive().TryRevokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-004/A4,D11: IsActive = false → ForbiddenException từ IIdentityService lan thẳng lên")]
    public async Task Handle_DisabledUser_PropagatesForbidden()
    {
        GivenStoredToken(DateTime.UtcNow.AddDays(1));
        _identityService
            .GetUserForRefreshAsync(UserId, Arg.Any<CancellationToken>())
            .Returns<AuthenticatedUser?>(_ => throw new ForbiddenException(ErrorCodes.AuthAccountDisabled, "disabled"));

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AuthAccountDisabled);
    }

    [Fact(DisplayName = "FR-AUTH-004/D35: thua race (request khác đã rotation) → 401 REVOKED, KHÔNG revoke family, không lưu RT mới")]
    public async Task Handle_LostRace_ThrowsRevokedWithoutFamilyRevoke()
    {
        GivenStoredToken(DateTime.UtcNow.AddDays(1));
        _refreshTokenRepository
            .TryRevokeAsync(OldHash, NewHash, Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AuthRefreshTokenRevoked);
        await _refreshTokenRepository.DidNotReceive().RevokeAllActiveForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _refreshTokenRepository.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
