using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Auth.Queries.GetCurrentUser;

/// <summary>
/// Returns the current user's profile, read from the database rather than from the token (ADR-21).
/// </summary>
public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    private readonly IUserQueries _userQueries;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserQueryHandler(IUserQueries userQueries, ICurrentUserService currentUser)
    {
        _userQueries = userQueries;
        _currentUser = currentUser;
    }

    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        // توكن صالح لمستخدم لا صفّ له: نادر، لكنه 404 صادق لا 500
        return await _userQueries.GetCurrentAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");
    }
}
