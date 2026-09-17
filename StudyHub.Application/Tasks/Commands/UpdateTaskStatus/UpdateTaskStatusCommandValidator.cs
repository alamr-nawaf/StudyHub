using FluentValidation;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskStatus;

/// <summary>
/// Validates <see cref="UpdateTaskStatusCommand"/>.
/// </summary>
public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        // التعداد رقم في الـ JSON، فقيمة 99 تمرّ بلا هذا السطر
        RuleFor(x => x.Status).IsInEnum();
    }
}
