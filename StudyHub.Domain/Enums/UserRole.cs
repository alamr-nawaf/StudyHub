namespace StudyHub.Domain.Enums;

/// <summary>
/// What kind of account this is. One column rather than a join table, because two roles that
/// do not overlap are fully described by one value (ADR-31).
/// </summary>
public enum UserRole
{
    User = 0,
    Admin = 1
}