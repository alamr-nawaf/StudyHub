using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Notes.Commands.CreateNote;
using Microsoft.AspNetCore.Authorization;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/notes")]
public class NotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateNoteCommand command,
        CancellationToken cancellationToken)
    {
        var noteId = await _mediator.Send(command, cancellationToken);
        return Created($"/api/notes/{noteId}", new { noteId });
    }
    [Authorize]
    [HttpGet("debug-claims")]
    public IActionResult DebugClaims() =>
    Ok(User.Claims.Select(c => new { c.Type, c.Value }));
}