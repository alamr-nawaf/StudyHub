namespace StudyHub.Infrastructure.Authentication;

/// <summary>
/// Everything needed to issue and to validate a token, in one place. The key is never
/// written into appsettings: it lives in user secrets (CODING_STANDARDS §11), and the two
/// lifetimes come from Requirements §9.1.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}