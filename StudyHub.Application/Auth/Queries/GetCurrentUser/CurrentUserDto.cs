using StudyHub.Domain.Enums;

namespace StudyHub.Application.Auth.Queries.GetCurrentUser;

/// <summary>
/// The caller's own profile and AI quota, returned by GET /api/auth/me.
/// </summary>
public record CurrentUserDto(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    int MonthlyTokenQuota,
    int TokensUsedThisMonth);
