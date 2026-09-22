using StudyHub.Domain.Enums;

namespace StudyHub.Domain.Authorization;

/// <summary>
/// Maps a role to what it may do. An ordinary user holds no administrative permission at
/// all: what they may do to their own data comes from ownership, not from their role.
/// </summary>
public static class RolePermissions
{
    // One shared empty set, so no allocation happens on every call
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private static readonly IReadOnlyDictionary<UserRole, IReadOnlySet<string>> Map =
        new Dictionary<UserRole, IReadOnlySet<string>>
        {
            [UserRole.User] = None,
            [UserRole.Admin] = new HashSet<string>
            {
                Permissions.UsersDeactivate
            }
        };

    public static IReadOnlySet<string> For(UserRole role) =>
        Map.TryGetValue(role, out var permissions)
            ? permissions
            : None;   // an unknown role holds nothing

    public static bool Has(UserRole role, string permission) =>
        For(role).Contains(permission);
}