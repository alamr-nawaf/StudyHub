using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Repositories;

// تنفيذ عقد توكنات التجديد فوق EF Core
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
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, utcNow), cancellationToken);
}