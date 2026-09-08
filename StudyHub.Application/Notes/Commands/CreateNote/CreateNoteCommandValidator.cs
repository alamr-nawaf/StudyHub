using FluentValidation;

namespace StudyHub.Application.Notes.Commands.CreateNote;

public class CreateNoteCommandValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);

        // الابن يرث كورس أبيه، فإرسال الاثنين معًا التباس لا تفضيل
        RuleFor(x => x.CourseId)
            .Null()
            .When(x => x.ParentItemId.HasValue)
            .WithMessage("A nested item inherits its parent's course.");
    }
}