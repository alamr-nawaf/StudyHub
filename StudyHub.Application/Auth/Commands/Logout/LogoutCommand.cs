using MediatR;

namespace StudyHub.Application.Auth.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest;