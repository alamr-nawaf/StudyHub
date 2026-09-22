using MediatR;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tasks.Commands.CreateTask;

/// <summary>
/// Creates a task for the caller. ParentItemId and CourseId are orthogonal: a root takes a
/// course, and a nested item inherits its parent's course rather than naming one.
/// </summary>
public record CreateTaskCommand(
    string Title,
    string? Content,
    Guid? ParentItemId,
    Guid? CourseId,
    TaskPriority Priority = TaskPriority.Medium,
    DateTime? DueDate = null) : IRequest<Guid>;