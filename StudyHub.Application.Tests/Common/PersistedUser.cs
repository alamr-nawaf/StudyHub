using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Common;

/// <summary>
/// Builds a <see cref="User"/> in a state only the database can produce. Since M8 the token
/// counter is raised by one atomic UPDATE (ADR-37) and the reset date only moves inside
/// <c>ResetQuotaIfNeeded</c>, so no public member can put a user mid-month. These tests set
/// the two properties the way EF Core does when it reads the row back.
/// </summary>
internal static class PersistedUser
{
    public static User With(int quota, int tokensUsed, DateTime? lastReset = null)
    {
        var user = User.Create("Quota Owner", "quota@test.com", "hash", quota);

        Set(user, nameof(User.TokensUsedThisMonth), tokensUsed);

        if (lastReset is not null)
            Set(user, nameof(User.LastTokenResetDate), lastReset.Value);

        return user;
    }

    private static void Set(User user, string propertyName, object value)
        => typeof(User).GetProperty(propertyName)!.SetValue(user, value);
}
