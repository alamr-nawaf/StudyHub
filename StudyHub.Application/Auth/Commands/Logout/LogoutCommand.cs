using MediatR;

namespace StudyHub.Application.Auth.Commands.Logout;

/// <summary>
/// Ends one session: the refresh token in the body is the one to revoke.
/// </summary>
public record LogoutCommand(string RefreshToken) : IRequest;