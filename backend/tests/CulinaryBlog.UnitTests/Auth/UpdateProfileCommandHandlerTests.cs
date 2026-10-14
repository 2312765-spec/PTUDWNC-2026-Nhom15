using CulinaryBlog.Application.Auth.Commands.UpdateProfile;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-007 — mọi nhánh của handler qua mock (D5, D48, D51).</summary>
public class UpdateProfileCommandHandlerTests
{
    private const string UserId = "user-1";

    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();

    private UpdateProfileCommandHandler CreateHandler() => new(_currentUser, _identityService);

    [Fact(DisplayName = "FR-AUTH-007/D5: user tồn tại → map đủ 6 trường từ kết quả của IIdentityService")]
    public async Task Handle_UserExists_MapsProfile()
    {
        _currentUser.UserId.Returns(UserId);
        _identityService.UpdateProfileAsync(UserId, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(UserId, "a@example.com", "Tên Mới", "https://x/a.png", "bio", ["Author"]));

        var result = await CreateHandler().Handle(
            new UpdateProfileCommand("Tên Mới", "https://x/a.png", "bio"), CancellationToken.None);

        result.Id.Should().Be(UserId);
        result.Email.Should().Be("a@example.com");
        result.DisplayName.Should().Be("Tên Mới");
        result.AvatarUrl.Should().Be("https://x/a.png");
        result.Bio.Should().Be("bio");
        result.Roles.Should().BeEquivalentTo(["Author"]);
    }

    [Fact(DisplayName = "FR-AUTH-007/NFR-SEC-006: UserId lấy từ ICurrentUser (JWT), truyền xuống IIdentityService")]
    public async Task Handle_UsesUserIdFromCurrentUser()
    {
        _currentUser.UserId.Returns(UserId);
        _identityService.UpdateProfileAsync(UserId, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(UserId, "a@example.com", "An", null, null, ["Author"]));

        await CreateHandler().Handle(new UpdateProfileCommand("An", null, null), CancellationToken.None);

        await _identityService.Received(1)
            .UpdateProfileAsync(UserId, "An", null, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-007/D51-2: displayName và avatarUrl được trim, bio giữ nguyên, null giữ null, \"\" giữ \"\" (lệnh xóa)")]
    public async Task Handle_TrimsNames_PreservesNullAndEmpty()
    {
        _currentUser.UserId.Returns(UserId);
        _identityService.UpdateProfileAsync(UserId, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(UserId, "a@example.com", "An", null, null, ["Author"]));

        await CreateHandler().Handle(new UpdateProfileCommand("  An  ", "  ", "  nhiều  khoảng  "), CancellationToken.None);

        // avatarUrl chỉ có khoảng trắng → trim thành "" = lệnh xóa; bio không bị đụng vào.
        await _identityService.Received(1)
            .UpdateProfileAsync(UserId, "An", "", "  nhiều  khoảng  ", Arg.Any<CancellationToken>());

        await CreateHandler().Handle(new UpdateProfileCommand(null, "", null), CancellationToken.None);

        await _identityService.Received(1)
            .UpdateProfileAsync(UserId, null, "", null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-AUTH-007/D48,D51-5: user không còn trong DB → NotFoundException USER_NOT_FOUND")]
    public async Task Handle_UserMissing_ThrowsNotFound()
    {
        _currentUser.UserId.Returns(UserId);
        _identityService.UpdateProfileAsync(UserId, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((AuthenticatedUser?)null);

        var act = () => CreateHandler().Handle(new UpdateProfileCommand("An", null, null), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
    }

    [Fact(DisplayName = "FR-AUTH-007: không có UserId trong claims → UnauthorizedException AUTH_TOKEN_INVALID, không chạm Identity")]
    public async Task Handle_NoUserId_ThrowsUnauthorized()
    {
        _currentUser.UserId.Returns((string?)null);

        var act = () => CreateHandler().Handle(new UpdateProfileCommand("An", null, null), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<UnauthorizedException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.AuthTokenInvalid);
        await _identityService.DidNotReceive().UpdateProfileAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
