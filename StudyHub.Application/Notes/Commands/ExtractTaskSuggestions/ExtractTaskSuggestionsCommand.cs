using MediatR;

namespace StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;

/// <summary>
/// Asks the AI provider which tasks one of the caller's notes implies. It carries only the
/// note id, and nothing is created: a suggestion becomes a task only when the user approves
/// it through POST /api/tasks (ADR-28).
/// </summary>
public record ExtractTaskSuggestionsCommand(Guid NoteId) : IRequest<AiTaskSuggestionsResult>;
