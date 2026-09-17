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
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.DueDate).UtcOrNull();
    }
}
