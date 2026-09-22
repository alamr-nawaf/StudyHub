using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Tasks.Commands.CreateTask;
using StudyHub.Application.Tasks.Commands.UpdateTaskSchedule;
using StudyHub.Application.Tasks.Commands.UpdateTaskStatus;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/tasks")]
/// <summary>
/// Tasks: creation, and the two partial updates of status and schedule.
/// </summary>
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTaskCommand command,
        CancellationToken cancellationToken)
    {
        var taskId = await _mediator.Send(command, cancellationToken);
        return Created($"/api/tasks/{taskId}", new { taskId });
    }

    // The id comes from the route, not from the body: `with` overwrites whatever id the
    // client sent
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateTaskStatusCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/schedule")]
    public async Task<IActionResult> UpdateSchedule(
        Guid id,
        UpdateTaskScheduleCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }
}
