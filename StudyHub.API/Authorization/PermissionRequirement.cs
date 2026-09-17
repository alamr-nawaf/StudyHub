using Microsoft.AspNetCore.Authorization;

namespace StudyHub.API.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
