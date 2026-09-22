using FluentValidation;
using StudyHub.Application.Common.Validation;

namespace StudyHub.Application.Users.Commands.RegisterUser;

/// <summary>
/// Validates <see cref="RegisterUserCommand"/>, including the password policy shared with
/// administrator seeding.
/// </summary>
public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(150);

        RuleFor(x => x.Password).StrongPassword();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}