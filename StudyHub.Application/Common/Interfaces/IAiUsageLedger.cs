namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// The one record of AI usage: the AiUsageLogs table. It answers how much a user has spent
/// since an instant, and records what a call cost as one inserted row (ADR-39). Recording
/// never refuses, because the call has already been paid for (ADR-24).
/// </summary>
public interface IAiUsageLedger
{
    Task<int> SumTokensSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken cancellationToken);

    Task RecordAsync(Guid userId, string operationType, int tokens, CancellationToken cancellationToken);
}
