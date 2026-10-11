using CulinaryBlog.Application.Auth.Queries.GetCurrentUser;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-006 — mọi nhánh của handler qua mock (D5, D48).</summary>
public class GetCurrentUserQueryHandlerTests
{
    private const string UserId = "user-1";

    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();

    private GetCurrentUserQueryHandler CreateHandler() => new(_currentUser, _identityService);

    [Fact(DisplayName = "FR-AUTH-006/D5: user tồn tại → map đủ 6 trường sang UserProfileDto")]
    public async Task Handle_UserExists_MapsProfile()
    {
        _currentUser.UserId.Returns(UserId);
        _identityService.GetUserByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(UserId, "a@example.com", "An", "https://x/a.png", "bio", ["Author"]));

        var result = await CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.Id.Should().Be(UserId);
        result.Email.Should().Be("a@example.com");
        result.DisplayName.Should().Be("An");
        result.AvatarUrl.Should().Be("https://x/a.png");
        result.Bio.Should().Be("bio");
        result.Roles.Should().BeEquivalentTo(["Author"]);
    }

    [Fact(DisplayName = "FR-AUTH-006/D48: user không còn trong DB → NotFoundException USER_NOT_FOUND")]
    public async Task Handle_UserMissing_ThrowsNotFound()
    {
        _currentUser.UserId.Returns(UserId);
        _identityService.GetUserByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns((AuthenticatedUser?)null);

        var act = () => CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
    }

    [Fact(DisplayName = "FR-AUTH-006: không có UserId trong claims → UnauthorizedException AUTH_TOKEN_INVALID")]
    public async Task Handle_NoUserId_ThrowsUnauthorized()
    {
        _currentUser.UserId.Returns((string?)null);

        var act = () => CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<UnauthorizedException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.AuthTokenInvalid);
        await _identityService.DidNotReceive().GetUserByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
