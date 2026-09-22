using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

/// <summary>
/// A course: the top of the content tree, and the only entity a note or a task can be
/// grouped under. Deleting one soft-deletes everything below it.
/// </summary>
public sealed class Course : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsDeleted { get; private set; }

    private Course() { }

    public static Course Create(Guid userId, string title, string? description = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Course title cannot be empty.");

        return new Course
        {
            UserId = userId,
            Title = title.Trim(),       
            Description = description,
            IsDeleted = false
        };
    }
    public void UpdateDetails(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Course title cannot be empty.");


        Title = title.Trim();
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }
    public void MarkAsDeleted()
    {
        // Tolerant, like Item.MarkAsDeleted: deleting an already-deleted course must not
        // re-stamp UpdatedAt with a second, misleading instant
        if (IsDeleted) return;

        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}