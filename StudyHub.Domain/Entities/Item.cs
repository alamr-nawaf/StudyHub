using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

// الأساس المشترك للملاحظة والمهمة. يسكن جدولًا واحدًا عبر نمط TPH.
public abstract class Item : AuditableEntity
{
    // خمسة مستويات: من صفر إلى أربعة
    public const int MaxDepth = 4;

    public Guid UserId { get; private set; }
    public Guid? CourseId { get; private set; }
    public Guid? ParentItemId { get; private set; }
    public int Depth { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Content { get; private set; }
    public bool IsDeleted { get; private set; }

    protected Item() { }

    // تُستدعى من دوال الإنشاء في الأصناف المشتقة فقط
    protected void Initialize(Guid userId, string title, string? content, Item? parent, Guid? courseId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.");

        if (parent is null)
        {
            // جذر: ينتمي لكورس أو لا ينتمي لشيء
            ParentItemId = null;
            CourseId = courseId;
            Depth = 0;
        }
        else
        {
            // خط الدفاع الأخير عن الملكية — داخل الكيان لا في المعالِج
            if (parent.UserId != userId)
                throw new InvalidOperationException("Parent belongs to another user.");

            if (parent.IsDeleted)
                throw new InvalidOperationException("Cannot nest under a deleted item.");

            if (parent.Depth >= MaxDepth)
                throw new InvalidOperationException("Maximum nesting depth reached.");

            ParentItemId = parent.Id;
            CourseId = parent.CourseId;   // تكرار متعمّد: يجعل حذف الكورس جملة واحدة
            Depth = parent.Depth + 1;
        }

        UserId = userId;
        Title = title.Trim();
        Content = content;
        IsDeleted = false;
    }

    public void UpdateContent(string title, string? content)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.");

        Title = title.Trim();
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsDeleted()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}