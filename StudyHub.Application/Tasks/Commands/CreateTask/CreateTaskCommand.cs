using MediatR;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tasks.Commands.CreateTask;

// ParentItemId و CourseId متعامدان: الجذر يأخذ كورسًا، والابن يرث كورس أبيه
public record CreateTaskCommand(
    string Title,
    string? Content,
    Guid? ParentItemId,
    Guid? CourseId,
    TaskPriority Priority = TaskPriority.Medium,
    DateTime? DueDate = null) : IRequest<Guid>;