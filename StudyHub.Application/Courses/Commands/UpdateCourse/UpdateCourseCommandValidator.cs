using FluentValidation;

namespace StudyHub.Application.Courses.Commands.UpdateCourse;

/// <summary>
/// Validates <see cref="UpdateCourseCommand"/> with the same limits as course creation.
/// </summary>
public class UpdateCourseCommandValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
