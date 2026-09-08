using FluentAssertions;
using StudyHub.Domain.Entities;

namespace StudyHub.Domain.Tests;

public class RefreshTokenTests
{
    private static RefreshToken CreateSut(DateTime expiresAt)
        => RefreshToken.Create(Guid.NewGuid(), "hashed-token", expiresAt);

    [Fact]
    public void IsActive_WhenNotExpiredAndNotRevoked_ShouldReturnTrue()
    {
        var now = DateTime.UtcNow;
        var token = CreateSut(now.AddDays(7));

        token.IsActive(now).Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldReturnFalse()
    {
        var now = DateTime.UtcNow;
        var token = CreateSut(now.AddDays(-1));

        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void Revoke_OnActiveToken_ShouldMarkRevokedAndLinkReplacement()
    {
        var now = DateTime.UtcNow;
        var token = CreateSut(now.AddDays(7));
        var newTokenId = Guid.NewGuid();

        token.Revoke(now, newTokenId);

        token.RevokedAt.Should().Be(now);
        token.ReplacedByTokenId.Should().Be(newTokenId);
        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void Revoke_OnAlreadyRevokedToken_ShouldThrowInvalidOperationException()
    {
        var now = DateTime.UtcNow;
        var token = CreateSut(now.AddDays(7));
        token.Revoke(now);

        var act = () => token.Revoke(now);

        act.Should().Throw<InvalidOperationException>();
    }
}