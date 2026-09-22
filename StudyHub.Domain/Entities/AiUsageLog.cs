using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

/// <summary>
/// One recorded AI call, and since M8.1 the only record of a user's usage (ADR-39). It
/// inherits BaseEntity rather than AuditableEntity: the row is written once and never edited.
/// </summary>
public sealed class AiUsageLog : BaseEntity
{
    public Guid UserId { get; private set; }
    public string OperationType { get; private set; } = string.Empty;
    public int TokensConsumed { get; private set; }

    private AiUsageLog() { }

    public static AiUsageLog Create(Guid userId, string operationType, int tokensConsumed)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.");

        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException("Operation type cannot be empty.");

        if (tokensConsumed <= 0)
            throw new ArgumentException("Tokens consumed must be positive.");

        return new AiUsageLog
        {
            UserId = userId,
            OperationType = operationType.Trim(),
            TokensConsumed = tokensConsumed
        };
        // The instant of the call is the inherited CreatedAt: a separate field would only
        // have repeated it, because both were always filled with the same value.
    }
}