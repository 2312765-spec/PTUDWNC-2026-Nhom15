using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Domain;

/// <summary>SRS 7.8 / D20 — IsRevoked là computed từ RevokedAt, không phải cột DB.</summary>
public class RefreshTokenTests
{
    [Fact(DisplayName = "D20: token mới tạo còn hiệu lực (chưa revoke, chưa hết hạn)")]
    public void Create_NewToken_IsActive()
    {
        var token = RefreshToken.Create("user-1", "hash", DateTime.UtcNow.AddDays(7), "127.0.0.1");

        token.IsRevoked.Should().BeFalse();
        token.IsExpired.Should().BeFalse();
        token.IsActive.Should().BeTrue();
        token.UserId.Should().Be("user-1");
        token.TokenHash.Should().Be("hash");
    }

    [Fact(DisplayName = "D20: Revoke() đặt RevokedAt, IsRevoked trở thành true (computed)")]
    public void Revoke_SetsRevokedAt()
    {
        var token = RefreshToken.Create("user-1", "hash", DateTime.UtcNow.AddDays(7), null);

        token.Revoke("new-hash");

        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
        token.ReplacedByTokenHash.Should().Be("new-hash");
    }

    [Fact(DisplayName = "D20: token hết hạn (ExpiresAt trong quá khứ) không còn active dù chưa revoke")]
    public void ExpiredToken_IsNotActive()
    {
        var token = RefreshToken.Create("user-1", "hash", DateTime.UtcNow.AddSeconds(-1), null);

        token.IsExpired.Should().BeTrue();
        token.IsRevoked.Should().BeFalse();
        token.IsActive.Should().BeFalse();
    }
}
