namespace StudyHub.API.Common;

/// <summary>
/// One fixed window: how many requests a partition may spend, and over how many seconds.
/// </summary>
public record RateLimitWindowSettings(int PermitLimit, int WindowSeconds);

/// <summary>
/// The two rate-limiting policies of ADR-45, under the "RateLimiting" section: "Auth" for
/// the anonymous credential endpoints, partitioned by client address, and "Ai" for the two
/// paid AI operations, partitioned by user. Rate limiting is an API concern, so the record
/// lives here rather than in Application.
/// </summary>
public record RateLimitingSettings(RateLimitWindowSettings Auth, RateLimitWindowSettings Ai)
{
    public const string SectionName = "RateLimiting";

    public const string AuthPolicy = "auth";
    public const string AiPolicy = "ai";
}
