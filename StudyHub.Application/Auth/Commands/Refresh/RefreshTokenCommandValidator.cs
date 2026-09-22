using FluentValidation;

namespace StudyHub.Application.Auth.Commands.Refresh;

/// <summary>
/// Presence only. No length and no format rule: a forged token is a rejected credential
/// (401), not a malformed request (400).
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}