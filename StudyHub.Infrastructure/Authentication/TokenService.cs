using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Enums;

namespace StudyHub.Infrastructure.Authentication;

// يصنع توكن الوصول الموقّع، وتوكن التجديد العشوائي، وهاشه الحتمي.
// القواعد من المتطلبات §9.1: ثلاثة claims لا غير، وHS256، والمدّة من الإعدادات
public sealed class TokenService : ITokenService
{
    // المعالِج عديم الحالة وآمن للتوازي — نسخة واحدة تكفي التطبيق كله
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

            // قاموس لا ClaimsIdentity: الأسماء تُكتب حرفيًا كما هنا
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