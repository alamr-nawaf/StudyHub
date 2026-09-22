using FluentValidation;

namespace StudyHub.Application.Auth.Commands.Logout;

/// <summary>
/// The token must be present; whether it is genuine is the handler's business.
/// </summary>
public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}