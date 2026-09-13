using StudyHub.Domain.Enums;

namespace StudyHub.Domain.Authorization;

// خريطة الدور إلى قدراته. المستخدم العادي لا يملك أي صلاحية إدارية —
// صلاحياته على بياناته تأتي من الملكية لا من الدور
public static class RolePermissions
{
    // مجموعة فارغة واحدة مشتركة: لا تخصيص جديد في كل نداء
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
            : None;   // دور غير معروف لا يملك شيئًا

    public static bool Has(UserRole role, string permission) =>
        For(role).Contains(permission);
}