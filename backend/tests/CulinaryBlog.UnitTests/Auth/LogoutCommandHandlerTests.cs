using CulinaryBlog.Application.Auth.Commands.Logout;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-005 — mọi nhánh của handler qua mock (D20, D35-6, D47).</summary>
public class LogoutCommandHandlerTests
{
    private const string RawToken = "raw-token";
    private const string TokenHash = "hash-token";
    private const string UserId = "user-1";

    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();

    public LogoutCommandHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _jwtService.HashToken(RawToken).Returns(TokenHash);
    }

    private LogoutCommandHandler CreateHandler()
        => new(_currentUser, _jwtService, _refreshTokenRepository, NullLogger<LogoutCommandHandler>.Instance);

    private void GivenToken(RefreshToken? token)
        => _refreshTokenRepository.GetByTokenAsync(TokenHash, Arg.Any<CancellationToken>()).Returns(token);

    private async Task AssertNotRevokedAsync()
        => await _refreshTokenRepository.DidNotReceive()
            .TryRevokeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

    [Fact(DisplayName = "FR-AUTH-005/D20: token hợp lệ của user hiện tại → TryRevoke theo hash, không có token thay thế")]
    public async Task Handle_OwnActiveToken_Revokes()
    {
        GivenToken(RefreshToken.Create(UserId, TokenHash, DateTime.UtcNow.AddDays(7)));

        await CreateHandler().Handle(new LogoutCommand(RawToken), CancellationToken.None);

        await _refreshTokenRepository.Received(1).TryRevokeAsync(TokenHash, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-005/A1: token không tồn tại → no-op, không ném")]
    public async Task Handle_UnknownToken_IsNoOp()
    {
        GivenToken(null);

        await CreateHandler().Handle(new LogoutCommand(RawToken), CancellationToken.None);

        await AssertNotRevokedAsync();
    }

    [Fact(DisplayName = "FR-AUTH-005/D47: token của user khác → no-op")]
    public async Task Handle_TokenOfAnotherUser_IsNoOp()
    {
        GivenToken(RefreshToken.Create("other-user", TokenHash, DateTime.UtcNow.AddDays(7)));

        await CreateHandler().Handle(new LogoutCommand(RawToken), CancellationToken.None);

        await AssertNotRevokedAsync();
    }

    [Fact(DisplayName = "FR-AUTH-005/D47: token đã revoke → no-op, không kích hoạt reuse detection")]
    public async Task Handle_AlreadyRevokedToken_IsNoOp()
    {
        var token = RefreshToken.Create(UserId, TokenHash, DateTime.UtcNow.AddDays(7));
        token.Revoke();
        GivenToken(token);

        await CreateHandler().Handle(new LogoutCommand(RawToken), CancellationToken.None);

        await AssertNotRevokedAsync();
        await _refreshTokenRepository.DidNotReceive()
            .RevokeAllActiveForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-005/D47: token đã hết hạn → no-op")]
    public async Task Handle_ExpiredToken_IsNoOp()
    {
        GivenToken(RefreshToken.Create(UserId, TokenHash, DateTime.UtcNow.AddMinutes(-1)));

        await CreateHandler().Handle(new LogoutCommand(RawToken), CancellationToken.None);

        await AssertNotRevokedAsync();
    }

    [Fact(DisplayName = "FR-AUTH-005/D35-6: thua race (TryRevoke trả false) → không ném, vẫn thành công")]
    public async Task Handle_LostRace_DoesNotThrow()
    {
        GivenToken(RefreshToken.Create(UserId, TokenHash, DateTime.UtcNow.AddDays(7)));
        _refreshTokenRepository
            .TryRevokeAsync(TokenHash, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => CreateHandler().Handle(new LogoutCommand(RawToken), CancellationToken.None);

        await act();
    }
}
