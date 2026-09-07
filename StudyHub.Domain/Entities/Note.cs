using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

public sealed class Note : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid? CourseId { get; private set; }
    public string? Title { get; private set; }
    public string? Content { get; private set; }
    public bool IsDeleted { get; private set; }

    // مُنشئ خاص (Private Constructor) مطلوب لعمل Entity Framework Core
    private Note() { }

    // دالة الإنشاء الآمنة (Factory Method) لضمان جودة البيانات
    public static Note Create(Guid userId, string? title, string? content, Guid? courseId = null)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("A note must have at least a title or content.");
        }

        return new Note
        {
            UserId = userId,
            CourseId = courseId,
            Title = title,
            Content = content,
            IsDeleted = false
        };
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
