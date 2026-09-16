namespace StudyHub.Domain.Authorization;

// أسماء الصلاحيات كثوابت: الدومين يملكها، وASP.NET Core يستهلكها كأسماء policies —
// نص عادي، فلا اعتمادية تدخل الدومين (ADR-31)
public static class Permissions
{
    public const string UsersDeactivate = "users:deactivate";
}