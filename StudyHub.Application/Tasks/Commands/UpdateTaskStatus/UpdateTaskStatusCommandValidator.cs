using FluentValidation;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskStatus;

/// <summary>
/// Validates <see cref="UpdateTaskStatusCommand"/>.
/// </summary>
public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        // NotNull before IsInEnum: an absent field is refused with a 400 instead of silently
        // becoming Pending (A24). And an enum is a number in JSON, so a value of 99 would pass
        // without IsInEnum
        RuleFor(x => x.Status)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .IsInEnum();
    }
}
