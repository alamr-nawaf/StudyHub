using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Notes.Commands.CreateNote;
using StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;
using StudyHub.Application.Notes.Commands.SummarizeNote;

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

    // Empty body: the note is named by the route and its text is already stored (§15)
    [HttpPost("{id:guid}/summarize")]
    public async Task<IActionResult> Summarize(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SummarizeNoteCommand(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/extract-tasks")]
    public async Task<IActionResult> ExtractTasks(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExtractTaskSuggestionsCommand(id), cancellationToken);
        return Ok(result);
    }
}