using MediatR;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskSchedule;

/// <summary>
/// Replaces a task's priority and due date; a null due date clears it.
/// </summary>
// The id always comes from the route: the controller overwrites whatever the body carried.
// Priority is nullable so that an absent field can be told apart from the value 0: without
// that, omitting it would silently mean Low (A24).
// DueDate is the opposite case: there, null is a deliberate value that clears the date
public record UpdateTaskScheduleCommand(Guid Id, TaskPriority? Priority, DateTime? DueDate) : IRequest;
