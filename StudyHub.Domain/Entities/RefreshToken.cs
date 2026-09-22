using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

/// <summary>
/// One refresh token of one session. Rotation replaces it with a successor and revokes it,
/// and the chain that links the two is what makes a replayed token recognisable (§9.3).
/// </summary>
public sealed class RefreshToken : AuditableEntity
{
    public Guid UserId { get; private set; }

    // The hash is stored, never the raw token: a leaked database then hands an attacker
    // no usable session
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    // Links a revoked token to the one that replaced it, which is what makes the rotation
    // chain traceable — and a replay of a rotated token detectable
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

    // Active = neither revoked nor expired
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