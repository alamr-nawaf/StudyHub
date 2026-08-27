using StudyHub.Domain.Common;
using StudyHub.Domain.Enums;

namespace StudyHub.Domain.Entities;

public sealed class TaskItem : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid? CourseId { get; private set; }
    public Guid? SourceNoteId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public StudyTaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime? DueDate { get; private set; }
    public bool IsDeleted { get; private set; }

    private TaskItem() { }

    public static TaskItem Create(Guid userId, string title, Guid? sourceNoteId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title cannot be empty.");

        return new TaskItem
        {
            UserId = userId,
            Title = title,
            SourceNoteId = sourceNoteId,
            Status = StudyTaskStatus.Pending,
            Priority = TaskPriority.Medium,
            IsDeleted = false
        };
    }
}