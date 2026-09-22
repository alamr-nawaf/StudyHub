using FluentValidation;
using StudyHub.Application.Common.Validation;

namespace StudyHub.Application.Users.Commands.SeedAdministrator;

/// <summary>
/// Validates <see cref="SeedAdministratorCommand"/>: malformed seed configuration stops
/// startup rather than creating a weak administrator.
/// </summary>
public class SeedAdministratorCommandValidator : AbstractValidator<SeedAdministratorCommand>
{
    public SeedAdministratorCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(150);

        // Conditional: an existing account is promoted without a password, but whatever is
        // written in the configuration obeys the same policy as registration — an
        // administrator account would be the weakest of all if it were allowed what everyone
        // else is refused
        RuleFor(x => x.Password!)
            .StrongPassword()
            .When(x => x.Password is not null);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(150)
            .When(x => x.FullName is not null);
    }
}
