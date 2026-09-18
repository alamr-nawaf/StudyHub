namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Records what an AI call cost: the atomic increment of the user's counter and the
/// AiUsageLog row, in one transaction (ADR-16, ADR-37). It is the record, not the rule —
/// it never refuses, because the call has already been paid for (ADR-24).
/// </summary>
public interface IAiUsageRecorder
{
    Task RecordAsync(Guid userId, string operationType, int tokens, CancellationToken cancellationToken);
}
