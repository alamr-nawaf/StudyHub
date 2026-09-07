using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

public sealed class RefreshToken : AuditableEntity
{
    public Guid UserId { get; private set; }

    // نخزّن الهاش لا النص الواضح: تسريب قاعدة البيانات لا يعطي المهاجم جلسات صالحة
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    // يربط التوكن الملغى بالذي حلّ محلّه — لتتبّع سلسلة التدوير
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty.");

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };
    }

    // صالح = لم يُلغَ ولم تنتهِ مدّته
    public bool IsActive(DateTime utcNow) => RevokedAt is null && utcNow < ExpiresAt;

    public void Revoke(DateTime utcNow, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
            throw new InvalidOperationException("Token is already revoked.");

        RevokedAt = utcNow;
        ReplacedByTokenId = replacedByTokenId;
        UpdatedAt = utcNow;
    }
}