using StudyHub.Domain.Enums;

namespace StudyHub.Domain.Entities;

public sealed class TaskItem : Item
{
    public StudyTaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime? DueDate { get; private set; }

    private TaskItem() { }

    public static TaskItem Create(
        Guid userId,
        string title,
        string? content = null,
        Item? parent = null,
        Guid? courseId = null,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null)
    {
        var task = new TaskItem();
        task.Initialize(userId, title, content, parent, courseId);
        task.Status = StudyTaskStatus.Pending;
        task.Priority = priority;
        task.DueDate = dueDate;
        return task;
    }

    public void UpdateStatus(StudyTaskStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSchedule(TaskPriority priority, DateTime? dueDate)
    {
        Priority = priority;
        DueDate = dueDate;
        UpdatedAt = DateTime.UtcNow;
    }
}