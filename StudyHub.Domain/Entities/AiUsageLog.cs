using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

// يرث BaseEntity لا AuditableEntity: سجل يُكتب مرة ولا يُعدَّل أبدًا
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
        // وقت العملية = CreatedAt الموروث. كان الحقلان يُملآن باللحظة نفسها.
    }
}