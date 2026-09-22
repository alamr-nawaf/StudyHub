using MediatR;

namespace StudyHub.Application.Users.Commands.RegisterUser;

/// <summary>
/// Creates an account. ConfirmPassword is compared in the validator and never stored.
/// </summary>
public record RegisterUserCommand(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword) : IRequest<Guid>;