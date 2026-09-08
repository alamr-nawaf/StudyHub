using StudyHub.Domain.Common;
using StudyHub.Domain.ValueObjects;

namespace StudyHub.Domain.Entities;

public sealed class User : AuditableEntity
{
    public string FullName { get; private set; } = string.Empty;

    // null! لأن EF Core يستدعي المُنشئ الخاص أولًا ثم يملأ الخصائص
    public Email Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = string.Empty;
    public int MonthlyTokenQuota { get; private set; }
    public int TokensUsedThisMonth { get; private set; }
    public DateTime LastTokenResetDate { get; private set; }
    public bool IsActive { get; private set; }

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
            Email = Email.Create(email),   // التطبيع والتحقق في مكان واحد
            PasswordHash = passwordHash,
            MonthlyTokenQuota = monthlyQuota,
            TokensUsedThisMonth = 0,
            LastTokenResetDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    // استهلاك التوكنات — العدّاد وفحص الحدّ في مكان واحد لا يمكن تجاوزه
    public void ConsumeTokens(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Token amount must be positive.");

        if (TokensUsedThisMonth + amount > MonthlyTokenQuota)
            throw new InvalidOperationException("Monthly token quota exceeded.");

        TokensUsedThisMonth += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    // إعادة التعيين إذا دخلنا شهرًا جديدًا. الوقت يأتي من الخارج ليكون الاختبار ممكنًا.
    public void ResetQuotaIfNeeded(DateTime utcNow)
    {
        if (utcNow.Year == LastTokenResetDate.Year &&
            utcNow.Month == LastTokenResetDate.Month)
            return;

        TokensUsedThisMonth = 0;
        LastTokenResetDate = utcNow;
        UpdatedAt = utcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;   // عملية مُتسامحة: تعطيل المعطَّل ليس خطأ

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }
}