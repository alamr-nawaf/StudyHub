namespace StudyHub.Domain.Common;

// الأساس المشترك لكل الكيانات: معرّف ووقت إنشاء فقط
public abstract class BaseEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}