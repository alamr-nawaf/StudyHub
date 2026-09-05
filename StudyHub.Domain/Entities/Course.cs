using StudyHub.Domain.Common;

namespace StudyHub.Domain.Entities;

public sealed class Course : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsDeleted { get; private set; }

    private Course() { }

    public static Course Create(Guid userId, string title, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Course title cannot be empty.");

        return new Course
        {
            UserId = userId,
            Title = title,
            Description = description,
            IsDeleted = false
        };
    }
    public void UpdateDetails(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Course title cannot be empty.");

        Title = title;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }
    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}