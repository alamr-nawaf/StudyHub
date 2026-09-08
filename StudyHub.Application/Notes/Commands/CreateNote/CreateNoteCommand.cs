using MediatR;

namespace StudyHub.Application.Notes.Commands.CreateNote;

// ParentItemId و CourseId متعامدان: الجذر يأخذ كورسًا، والابن يرث كورس أبيه
public record CreateNoteCommand(
    string Title,
    string? Content,
    Guid? ParentItemId,
    Guid? CourseId) : IRequest<Guid>;