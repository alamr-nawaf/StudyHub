using FluentValidation;

namespace StudyHub.Application.Items.Commands.UpdateItemContent;

/// <summary>
/// Validates <see cref="UpdateItemContentCommand"/> with the same title limit as item creation.
/// </summary>
public class UpdateItemContentCommandValidator : AbstractValidator<UpdateItemContentCommand>
{
    public UpdateItemContentCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
    }
}
