using Microsoft.AspNetCore.Authorization;
using StudyHub.Domain.Authorization;
using StudyHub.Domain.Enums;

namespace StudyHub.API.Authorization;

// خريطة واحدة لمستدعيَين (§9.5): السياسة تسأل RolePermissions نفسها التي يسألها User.Can،
// فلا قائمة صلاحيات في طبقة الـ API يمكن أن تنحرف عنها
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var raw = context.User.FindFirst("role")?.Value;

        // الاسم حرفيًا فقط: Enum.TryParse يقبل "1" و"admin" أيضًا، والتوكن لا يُصدر إلا الاسم.
        // claim مجهول أو غائب لا يفشل بخطأ — لا يمنح شيئًا، فيصير 403
        if (Enum.TryParse<UserRole>(raw, out var role)
            && role.ToString() == raw
            && RolePermissions.Has(role, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
