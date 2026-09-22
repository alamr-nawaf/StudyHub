using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyHub.API.Common;
using StudyHub.Application.Notes.Commands.CreateNote;
using StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;
using StudyHub.Application.Notes.Commands.SummarizeNote;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/notes")]
/// <summary>
/// Notes: creation, and the two AI operations that read one (UC-06, UC-07).
/// </summary>
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

    // Partitioned by user: these two cost money per call, so one account must not be
    // able to spend the provider budget everyone shares (ADR-45)
    [EnableRateLimiting(RateLimitingSettings.AiPolicy)]
    // Empty body: the note is named by the route and its text is already stored (§15)
    [HttpPost("{id:guid}/summarize")]
    public async Task<IActionResult> Summarize(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SummarizeNoteCommand(id), cancellationToken);
        return Ok(result);
    }

    [EnableRateLimiting(RateLimitingSettings.AiPolicy)]
    [HttpPost("{id:guid}/extract-tasks")]
    public async Task<IActionResult> ExtractTasks(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExtractTaskSuggestionsCommand(id), cancellationToken);
        return Ok(result);
    }
}