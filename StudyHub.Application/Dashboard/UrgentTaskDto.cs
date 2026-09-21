using StudyHub.Domain.Enums;

namespace StudyHub.Application.Dashboard;

/// <summary>
/// One entry of the dashboard's urgent-task preview: a title and a date, not a whole task
/// (ADR-43). DueDate is not nullable, because a task without one is never urgent.
/// </summary>
public sealed record UrgentTaskDto(
    Guid Id,
    string Title,
    Guid? CourseId,
    StudyTaskStatus Status,
    TaskPriority Priority,
    DateTime DueDate,
    bool IsOverdue);
