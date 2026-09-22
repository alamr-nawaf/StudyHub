using MediatR;
using StudyHub.Application.Auth.Commands.Login;

namespace StudyHub.Application.Auth.Commands.Refresh;

/// <summary>
/// Exchanges a refresh token for a new pair. Anonymous by design: the token in the body is
/// itself the credential, and the access token it replaces has usually already expired.
/// </summary>
public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResult>;