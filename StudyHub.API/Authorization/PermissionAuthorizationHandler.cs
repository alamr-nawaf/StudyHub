using Microsoft.AspNetCore.Authorization;
using StudyHub.Domain.Authorization;
using StudyHub.Domain.Enums;

namespace StudyHub.API.Authorization;

/// <summary>
/// One map with two callers (§9.5): the policy asks the same RolePermissions that User.Can
/// asks, so there is no second list of permissions in the API layer that could drift from it.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var raw = context.User.FindFirst("role")?.Value;

        // The literal name only: Enum.TryParse would also accept "1" and "admin", while the
        // token never issues anything but the name. An unknown or absent claim does not fail
        // with an error — it simply grants nothing, which becomes a 403
        if (Enum.TryParse<UserRole>(raw, out var role)
            && role.ToString() == raw
            && RolePermissions.Has(role, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
