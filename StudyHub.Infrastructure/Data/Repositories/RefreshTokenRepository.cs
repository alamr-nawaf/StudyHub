using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="StudyHub.Application.Common.Interfaces.IRefreshTokenRepository"/>.
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly StudyHubDbContext _context;

    public RefreshTokenRepository(StudyHubDbContext context) => _context = context;

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => _context.RefreshTokens.Add(token);

    public Task RevokeAllForUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            // ExecuteUpdate bypasses the entity, so UpdatedAt is stamped by hand here exactly
            // as RefreshToken.Revoke would have stamped it
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, utcNow)
                .SetProperty(t => t.UpdatedAt, utcNow), cancellationToken);

    // A revoked row must outlive its revocation, not its expiry: reuse detection finds a
    // rotated-away token by reading it, so only ExpiresAt may decide here (ADR-46)
    public Task<int> DeleteExpiredBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        _context.RefreshTokens
            .Where(t => t.ExpiresAt < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}