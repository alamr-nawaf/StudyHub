using MediatR;

namespace StudyHub.Application.Notes.Commands.SummarizeNote;

/// <summary>
/// Asks the AI provider for a short summary of one of the caller's notes. It carries only
/// the note id: the text comes from the stored note, and nothing is written back (§15).
/// </summary>
public record SummarizeNoteCommand(Guid NoteId) : IRequest<AiSummaryResult>;
