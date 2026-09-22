namespace StudyHub.Application.Common.Settings;

/// <summary>
/// How the refresh-token cleanup job behaves, under the "RefreshTokenCleanup" section.
/// A row is deleted only once it has been expired for RetentionDaysAfterExpiry days:
/// reuse detection needs a rotated-away row to still be there when it is presented
/// again (ADR-46, §9.3).
/// </summary>
public record RefreshTokenCleanupSettings(bool Enabled, int IntervalHours, int RetentionDaysAfterExpiry)
{
    public const string SectionName = "RefreshTokenCleanup";
}
