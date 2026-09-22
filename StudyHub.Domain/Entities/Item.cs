using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

/// <summary>
/// The common base of a note and a task. Both live in one table through the TPH pattern,
/// which is what lets a parent point at either kind with a single foreign key.
/// </summary>
public abstract class Item : AuditableEntity
{
    // Five levels, 0 through 4
    public const int MaxDepth = 4;
    public bool IsAtMaxDepth => Depth >= MaxDepth;

    public Guid UserId { get; private set; }
    public Guid? CourseId { get; private set; }
    public Guid? ParentItemId { get; private set; }
    public int Depth { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Content { get; private set; }
    public bool IsDeleted { get; private set; }

    protected Item() { }

    // Called only from the factory methods of the derived types
    protected void Initialize(Guid userId, string title, string? content, Item? parent, Guid? courseId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.");

        if (parent is null)
        {
            // A root: it belongs to a course, or to nothing at all
            ParentItemId = null;
            CourseId = courseId;
            Depth = 0;
        }
        else
        {
            // The last line of defence for ownership, inside the entity rather than in a
            // handler that could forget it
            if (parent.UserId != userId)
                throw new InvalidOperationException("Parent belongs to another user.");

            if (parent.IsDeleted)
                throw new InvalidOperationException("Cannot nest under a deleted item.");

            if (parent.IsAtMaxDepth)
                throw new InvalidOperationException("Maximum nesting depth reached.");

            ParentItemId = parent.Id;
            CourseId = parent.CourseId;   // deliberate duplication: it makes deleting a
                                          // course, at any depth, one statement (ADR-09)
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