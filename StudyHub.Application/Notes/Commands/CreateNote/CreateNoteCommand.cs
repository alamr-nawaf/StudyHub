using MediatR;

namespace StudyHub.Application.Notes.Commands.CreateNote;

/// <summary>
/// Creates a note for the caller. ParentItemId and CourseId are orthogonal: a root takes a
/// course, and a nested item inherits its parent's course rather than naming one.
/// </summary>
public record CreateNoteCommand(
    string Title,
    string? Content,
    Guid? ParentItemId,
    Guid? CourseId) : IRequest<Guid>;