using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Storage of refresh tokens. Lookup is by hash because the raw token is never stored.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(RefreshToken token);

    // An unconditional mass revocation: it executes immediately and does not go through
    // IUnitOfWork (ADR-27)
    Task RevokeAllForUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    // Executes immediately, like RevokeAllForUserAsync: not through IUnitOfWork (ADR-27).
    // Only ExpiresAt decides, never RevokedAt (ADR-46)
    Task<int> DeleteExpiredBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}