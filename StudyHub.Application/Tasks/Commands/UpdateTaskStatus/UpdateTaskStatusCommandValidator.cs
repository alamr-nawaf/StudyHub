using FluentValidation;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskStatus;

/// <summary>
/// Validates <see cref="UpdateTaskStatusCommand"/>.
/// </summary>
public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        // NotNull قبل IsInEnum: الحقل الغائب يُرفض بـ 400 بدل أن يصير Pending بصمت.
        // والتعداد رقم في الـ JSON، فقيمة 99 تمرّ بلا IsInEnum
        RuleFor(x => x.Status)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .IsInEnum();
    }
}
