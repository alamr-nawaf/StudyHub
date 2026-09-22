using FluentValidation;
using StudyHub.Application.Common.Validation;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskSchedule;

/// <summary>
/// Validates <see cref="UpdateTaskScheduleCommand"/>, including the UTC-only rule for the due date.
/// </summary>
public class UpdateTaskScheduleCommandValidator : AbstractValidator<UpdateTaskScheduleCommand>
{
    public UpdateTaskScheduleCommandValidator()
    {
        // NotNull before IsInEnum: an absent field is refused with a 400 instead of silently
        // becoming Low (A24)
        RuleFor(x => x.Priority)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .IsInEnum();

        RuleFor(x => x.DueDate).UtcOrNull();
    }
}
