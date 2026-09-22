using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data;

/// <summary>
/// EF Core implementation of <see cref="IAiUsageLedger"/> over the AiUsageLogs table, the
/// only record of AI usage (ADR-39). The sum is served by the (UserId, CreatedAt) index, and
/// recording is one insert, which parallel operations can neither lose nor collide on.
/// </summary>
public sealed class AiUsageLedger : IAiUsageLedger
{
    private readonly StudyHubDbContext _context;

    public AiUsageLedger(StudyHubDbContext context) => _context = context;

    public async Task<int> SumTokensSinceAsync(
        Guid userId, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        // SUM over no rows is NULL in SQL, and EF Core throws when it materializes NULL
        // into a non-nullable int, so the sum is taken as int? and an empty month is 0
        var sum = await _context.AiUsageLogs
            .Where(log => log.UserId == userId && log.CreatedAt >= sinceUtc)
            .SumAsync(log => (int?)log.TokensConsumed, cancellationToken);

        return sum ?? 0;
    }

    public async Task RecordAsync(
        Guid userId, string operationType, int tokens, CancellationToken cancellationToken)
    {
        _context.AiUsageLogs.Add(AiUsageLog.Create(userId, operationType, tokens));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
