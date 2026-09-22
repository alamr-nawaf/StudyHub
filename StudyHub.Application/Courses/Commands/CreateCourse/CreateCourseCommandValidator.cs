using FluentValidation;

namespace StudyHub.Application.Courses.Commands.CreateCourse;

/// <summary>
/// Validates <see cref="CreateCourseCommand"/> against the column limits of the schema.
/// </summary>
public class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}