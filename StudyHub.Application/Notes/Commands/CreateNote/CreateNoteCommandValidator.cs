using FluentValidation;

namespace StudyHub.Application.Notes.Commands.CreateNote;

/// <summary>
/// Validates <see cref="CreateNoteCommand"/>, including the cross-field rule that a nested
/// note may not name a course of its own.
/// </summary>
public class CreateNoteCommandValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);

        // A nested item inherits its parent's course, so sending both is an ambiguity
        // rather than a preference
        RuleFor(x => x.CourseId)
            .Null()
            .When(x => x.ParentItemId.HasValue)
            .WithMessage("A nested item inherits its parent's course.");
    }
}