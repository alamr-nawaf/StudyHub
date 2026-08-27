using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

public sealed class AiUsageLog : BaseEntity
{
    public Guid UserId { get; private set; }
    public string OperationType { get; private set; } = string.Empty;
    public int TokensConsumed { get; private set; }
    public DateTime OperationDate { get; private set; }

    private AiUsageLog() { }

    public static AiUsageLog Create(Guid userId, string operationType, int tokensConsumed)
    {
        return new AiUsageLog
        {
            UserId = userId,
            OperationType = operationType,
            TokensConsumed = tokensConsumed,
            OperationDate = DateTime.UtcNow
        };
    }
}