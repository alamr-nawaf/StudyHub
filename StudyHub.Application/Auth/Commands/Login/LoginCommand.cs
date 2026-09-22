using MediatR;

namespace StudyHub.Application.Auth.Commands.Login;

/// <summary>
/// Credentials presented at login. The response is the token pair of <see cref="LoginResult"/>.
/// </summary>
public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;