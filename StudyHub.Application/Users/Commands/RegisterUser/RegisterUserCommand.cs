using MediatR;

namespace StudyHub.Application.Users.Commands.RegisterUser;

public record RegisterUserCommand(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword) : IRequest<Guid>;