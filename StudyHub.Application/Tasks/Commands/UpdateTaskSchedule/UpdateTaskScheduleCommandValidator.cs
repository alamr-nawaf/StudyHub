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
        // NotNull قبل IsInEnum: الحقل الغائب يُرفض بـ 400 بدل أن يصير Low بصمت
        RuleFor(x => x.Priority)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .IsInEnum();

        RuleFor(x => x.DueDate).UtcOrNull();
    }
}
