using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using StudyHub.Domain.Enums;
using StudyHub.Infrastructure.Authentication;

namespace StudyHub.Infrastructure.Tests.Authentication;

// Proves that the token carries the claims Requirements §9.1 fixes and nothing else, that its
// lifetime comes from configuration, and that the refresh hash is deterministic and exactly as
// long as its column
public class TokenServiceTests
{
    // An instant with no fractional seconds: the token's exp field is whole seconds, so any
    // fraction of one is lost
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly TokenService _service = new(Options.Create(new JwtSettings
    {
        Issuer = "StudyHub",
        Audience = "StudyHub.API",
        Key = new string('k', 48),
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    }));

    [Fact]
    public void GenerateAccessToken_ShouldCarrySubJtiAndRole()
    {
        var userId = Guid.NewGuid();

        var result = _service.GenerateAccessToken(userId, UserRole.Admin, UtcNow);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(result.Value);
        token.GetClaim("sub").Value.Should().Be(userId.ToString());
        token.GetClaim("jti").Value.Should().NotBeNullOrWhiteSpace();
        token.GetClaim("role").Value.Should().Be("Admin");
        token.Claims.Select(c => c.Type).Should().BeEquivalentTo(
            new[] { "sub", "jti", "role", "iss", "aud", "exp", "iat", "nbf" });
    }

    [Fact]
    public void GenerateAccessToken_ShouldExpireAfterConfiguredMinutes()
    {
        var result = _service.GenerateAccessToken(Guid.NewGuid(), UserRole.User, UtcNow);

        result.ExpiresAt.Should().Be(UtcNow.AddMinutes(15));

        var token = new JsonWebTokenHandler().ReadJsonWebToken(result.Value);
        token.ValidTo.Should().Be(UtcNow.AddMinutes(15));
    }

    [Fact]
    public void GenerateRefreshToken_TwoCalls_ShouldDiffer()
    {
        var first = _service.GenerateRefreshToken(UtcNow);
        var second = _service.GenerateRefreshToken(UtcNow);

        first.RawToken.Should().NotBeNullOrWhiteSpace();
        first.RawToken.Should().NotBe(second.RawToken);
        first.TokenHash.Should().Be(_service.HashRefreshToken(first.RawToken));
        first.ExpiresAt.Should().Be(UtcNow.AddDays(7));
    }

    [Fact]
    public void HashRefreshToken_SameInput_ShouldReturnSame64CharHex()
    {
        var raw = _service.GenerateRefreshToken(UtcNow).RawToken;

        var first = _service.HashRefreshToken(raw);
        var second = _service.HashRefreshToken(raw);

        first.Should().Be(second);
        first.Should().MatchRegex("^[0-9a-f]{64}$");
    }
}