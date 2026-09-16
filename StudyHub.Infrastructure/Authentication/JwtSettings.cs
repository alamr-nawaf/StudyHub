
// إعدادات إصدار التوكن والتحقق منه في مكان واحد. المفتاح لا يُكتب في appsettings أبدًا،
// بل في user-secrets (CODING_STANDARDS §11)، والمدّتان من المتطلبات §9.1
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}