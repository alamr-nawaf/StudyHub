using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

public sealed class User : BaseEntity
{
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public int MonthlyTokenQuota { get; private set; }
    public int TokensUsedThisMonth { get; private set; }
    public DateTime LastTokenResetDate { get; private set; }
    public bool IsActive { get; private set; }

    private User() { }

    public static User Create(string fullName, string email, string passwordHash, int monthlyQuota)
    {
        return new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = passwordHash,
            MonthlyTokenQuota = monthlyQuota,
            TokensUsedThisMonth = 0,
            LastTokenResetDate = DateTime.UtcNow,
            IsActive = true
        };
    }
}
