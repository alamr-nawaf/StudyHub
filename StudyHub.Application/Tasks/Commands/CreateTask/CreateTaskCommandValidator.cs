using FluentValidation;
using StudyHub.Application.Common.Validation;

namespace StudyHub.Application.Tasks.Commands.CreateTask;

/// <summary>
/// Validates <see cref="CreateTaskCommand"/>: the title, the cross-field course rule, the
/// enum range, and the UTC-only due date.
/// </summary>
public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);

        // A nested item inherits its parent's course, so sending both is an ambiguity
        // rather than a preference
        RuleFor(x => x.CourseId)
            .Null()
            .When(x => x.ParentItemId.HasValue)
            .WithMessage("A nested item inherits its parent's course.");

        // An enum is a number in JSON, so a value of 99 would pass without this line
        RuleFor(x => x.Priority).IsInEnum();

        RuleFor(x => x.DueDate).UtcOrNull();
    }
}