namespace StudyHub.Domain.Common;

// للكيانات التي تتغيّر بعد إنشائها فقط
public abstract class AuditableEntity : BaseEntity
{
    public DateTime? UpdatedAt { get; protected set; }
}