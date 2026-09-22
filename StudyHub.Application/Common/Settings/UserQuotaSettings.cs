namespace StudyHub.Application.Common.Settings;

/// <summary>
/// The monthly AI token quota a newly created account starts with, under the
/// "UserQuota" section. It was a constant in the registration handler until M8.
/// </summary>
public record UserQuotaSettings(int DefaultMonthlyTokens)
{
    public const string SectionName = "UserQuota";
}
