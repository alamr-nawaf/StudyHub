using FluentValidation;

namespace StudyHub.Application.Auth.Commands.Login;

/// <summary>
/// Shape only: what login refuses here is a malformed request, never a wrong credential.
/// </summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Required: Email.Create throws on a malformed address, so without this rule the
        // response would be a 500 instead of a 400
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(150);

        // No complexity rules here: login compares against what is stored, it does not
        // enforce a policy. A minimum length would refuse a correct older password with a
        // 400 instead of letting it through
        RuleFor(x => x.Password).NotEmpty();
    }
}