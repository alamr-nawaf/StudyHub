using Microsoft.AspNetCore.Authorization;

namespace StudyHub.API.Authorization;

/// <summary>
/// The permission an endpoint's policy demands, as an authorization requirement.
/// </summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
