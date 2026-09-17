using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyHub.Domain.Authorization;

namespace StudyHub.API.Authorization;

public static class PermissionPolicies
{
    // سياسة لكل ثابت في Permissions، باسم الصلاحية نفسه. بالانعكاس لا بقائمة:
    // إضافة قدرة تبقى ثابتًا وسطرًا في الخريطة وسمة على الـ endpoint (ADR-31) — لا موضع رابع يُنسى،
    // وسياسة غير مسجّلة لا تفشل بأمان بل ترمي 500 عند أول طلب
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        var permissions = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        foreach (var permission in permissions)
        {
            options.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }
    }
}
