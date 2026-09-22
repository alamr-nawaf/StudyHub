namespace StudyHub.Domain.Authorization;

/// <summary>
/// The permission names as constants. The Domain owns them and ASP.NET Core consumes them
/// as policy names — plain strings, so no dependency enters the Domain (ADR-31).
/// </summary>
public static class Permissions
{
    public const string UsersDeactivate = "users:deactivate";
}