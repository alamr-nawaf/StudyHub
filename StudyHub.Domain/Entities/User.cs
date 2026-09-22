using StudyHub.Domain.Authorization;
using StudyHub.Domain.Common;
using StudyHub.Domain.Enums;
using StudyHub.Domain.ValueObjects;
namespace StudyHub.Domain.Entities;

/// <summary>
/// An account: who owns every course, item and AI call in the system.
/// </summary>
public sealed class User : AuditableEntity
{
    public string FullName { get; private set; } = string.Empty;

    // null! because EF Core calls the private constructor first and fills the properties
    // afterwards, so the compiler cannot see that this is ever assigned
    public Email Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = string.Empty;
    public int MonthlyTokenQuota { get; private set; }
    public bool IsActive { get; private set; }
    public UserRole Role { get; private set; }

    private User() { }

    public static User Create(string fullName, string email, string passwordHash, int monthlyQuota)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name cannot be empty.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        if (monthlyQuota <= 0)
            throw new ArgumentException("Monthly quota must be positive.");

        return new User
        {
            FullName = fullName.Trim(),
            Email = Email.Create(email),   // normalization and validation in one place
            PasswordHash = passwordHash,
            MonthlyTokenQuota = monthlyQuota,
            IsActive = true,
             Role = UserRole.User
        };
    }

    // The rule only, asked before every paid call (ADR-24). It changes nothing and
    // throws nothing: the month's usage is summed from AiUsageLogs by the caller, because
    // the log is the only record of spending and User keeps no copy of it (ADR-39)
    public bool HasQuotaFor(int tokensUsedThisMonth, int estimatedTokens)
        => tokensUsedThisMonth + estimatedTokens <= MonthlyTokenQuota;

    public void Deactivate()
    {
        if (!IsActive) return;   // tolerant: deactivating an inactive account is not an error

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
    // The only path to Admin, named loudly so that it shows up in any review. Demotion is
    // not supported until a real need asks for it (Requirements §12)
    public void PromoteToAdmin()
    {
        if (Role == UserRole.Admin) return;   // tolerant, like Deactivate

        Role = UserRole.Admin;
        UpdatedAt = DateTime.UtcNow;
    }

    // The rule is asked in one place, the same shape as IsAtMaxDepth (ADR-30)
    public bool Can(string permission) => RolePermissions.Has(Role, permission);

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }
}