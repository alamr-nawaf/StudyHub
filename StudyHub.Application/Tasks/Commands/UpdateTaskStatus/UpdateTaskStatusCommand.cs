using MediatR;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskStatus;

/// <summary>
/// Sets the status of a task.
/// </summary>
// The id always comes from the route: the controller overwrites whatever the body carried.
// Status is nullable so that an absent field can be told apart from the value 0: without
// that, omitting it would silently mean Pending (A24)
public record UpdateTaskStatusCommand(Guid Id, StudyTaskStatus? Status) : IRequest;
