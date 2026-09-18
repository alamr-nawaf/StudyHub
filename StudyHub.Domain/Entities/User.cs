using StudyHub.Domain.Authorization;
using StudyHub.Domain.Common;
using StudyHub.Domain.Enums;
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
            Email = Email.Create(email),   // التطبيع والتحقق في مكان واحد
            PasswordHash = passwordHash,
            MonthlyTokenQuota = monthlyQuota,
            TokensUsedThisMonth = 0,
            LastTokenResetDate = DateTime.UtcNow,
            IsActive = true,
             Role = UserRole.User
        };
    }

    // The rule only, asked before every paid call (ADR-24). It changes nothing and
    // throws nothing: the spending itself is recorded in SQL, so that two parallel
    // operations cannot lose an increment and neither can fail on its accounting (ADR-37)
    public bool HasQuotaFor(int estimatedTokens)
        => TokensUsedThisMonth + estimatedTokens <= MonthlyTokenQuota;

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
    // المسار الوحيد إلى Admin، ومسمّى بصوت عالٍ ليظهر في أي مراجعة.
    // التنزيل غير مدعوم حتى تطلبه حاجة حقيقية (Requirements §12)
    public void PromoteToAdmin()
    {
        if (Role == UserRole.Admin) return;   // عملية مُتسامحة مثل Deactivate

        Role = UserRole.Admin;
        UpdatedAt = DateTime.UtcNow;
    }

    // القاعدة تُسأل من مكان واحد — نفس شكل IsAtMaxDepth (ADR-30)
    public bool Can(string permission) => RolePermissions.Has(Role, permission);

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }
}