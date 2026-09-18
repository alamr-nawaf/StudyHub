using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data;

/// <summary>
/// Writes what an AI call cost: the counter on the user row and the AiUsageLog row, in one
/// transaction (ADR-16). The counter is raised with a single atomic UPDATE rather than by
/// loading the user and saving them back, so two parallel operations cannot lose an
/// increment and neither of them needs a concurrency token or a retry (ADR-37).
/// </summary>
public sealed class AiUsageRecorder : IAiUsageRecorder
{
    private readonly StudyHubDbContext _context;

    public AiUsageRecorder(StudyHubDbContext context) => _context = context;

    public async Task RecordAsync(
        Guid userId, string operationType, int tokens, CancellationToken cancellationToken)
    {
        // ExecuteUpdateAsync runs its own statement immediately, outside the change
        // tracker, so the log row would otherwise be saved by a separate transaction:
        // the two must land together or the counter and the history drift apart.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // UPDATE "Users" SET "TokensUsedThisMonth" = "TokensUsedThisMonth" + n WHERE "Id" = …
        // The new value is computed by PostgreSQL from the stored one, never from a value
        // this process read earlier. UpdatedAt is deliberately untouched: this is an
        // accounting record, not an edit the user made.
        await _context.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    user => user.TokensUsedThisMonth,
                    user => user.TokensUsedThisMonth + tokens),
                cancellationToken);

        _context.AiUsageLogs.Add(AiUsageLog.Create(userId, operationType, tokens));
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
