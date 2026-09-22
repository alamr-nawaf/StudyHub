using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyHub.Domain.Authorization;

namespace StudyHub.API.Authorization;

/// <summary>
/// Registers one policy per constant in Permissions, named after the permission itself.
/// </summary>
public static class PermissionPolicies
{
    // By reflection rather than from a list: adding a capability then stays a constant, a line
    // in the map and an attribute on the endpoint (ADR-31) — there is no fourth place to
    // forget. And an unregistered policy does not fail safely: it throws a 500 on the first
    // request that asks for it
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
