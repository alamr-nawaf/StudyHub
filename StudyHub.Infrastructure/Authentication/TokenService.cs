using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Enums;

namespace StudyHub.Infrastructure.Authentication;

/// <summary>
/// Makes the signed access token, the random refresh token and its deterministic hash. The
/// rules are those of Requirements §9.1: three claims and no more, HS256, and the lifetime
/// from configuration.
/// </summary>
public sealed class TokenService : ITokenService
{
    // The handler is stateless and thread-safe, so one instance serves the whole application
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public AccessToken GenerateAccessToken(Guid userId, UserRole role, DateTime utcNow)
    {
        var expiresAt = utcNow.AddMinutes(_settings.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = expiresAt,

            // A dictionary rather than a ClaimsIdentity: the names are written exactly as
            // they appear here, with no inbound mapping to long URIs
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["role"] = role.ToString()
            },

            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(Handler.CreateToken(descriptor), expiresAt);
    }

    public RefreshTokenResult GenerateRefreshToken(DateTime utcNow)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new RefreshTokenResult(raw, HashRefreshToken(raw),
                                      utcNow.AddDays(_settings.RefreshTokenDays));
    }
    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))
               .ToLowerInvariant();
}