using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Tasks.Commands.CreateTask;
using Microsoft.AspNetCore.Authorization;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/tasks")]
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
    [Authorize]
    [HttpGet("debug-claims")]
    public IActionResult DebugClaims() =>
    Ok(User.Claims.Select(c => new { c.Type, c.Value }));
}