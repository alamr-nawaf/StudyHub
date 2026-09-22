using MediatR;

namespace StudyHub.Application.Auth.Queries.GetCurrentUser;

/// <summary>
/// Requests the profile of the user who owns the access token.
/// </summary>
public record GetCurrentUserQuery : IRequest<CurrentUserDto>;
