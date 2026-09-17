using StudyHub.Domain.Enums;

namespace StudyHub.Application.Items;

/// <summary>
/// The shape of a note or a task returned by the API; the task fields are null for a note.
/// </summary>
public record ItemDto(
    Guid Id,
    Guid? ParentItemId,
    Guid? CourseId,
    int Depth,
    int Kind,
    string Title,
    string? Content,
    StudyTaskStatus? Status,
    TaskPriority? Priority,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
