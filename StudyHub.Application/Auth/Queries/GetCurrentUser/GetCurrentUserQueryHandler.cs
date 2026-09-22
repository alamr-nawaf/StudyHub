using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Time;

namespace StudyHub.Application.Auth.Queries.GetCurrentUser;

/// <summary>
/// Returns the current user's profile, read from the database rather than from the token (ADR-21).
/// </summary>
public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    private readonly IUserQueries _userQueries;
    private readonly ICurrentUserService _currentUser;
    private readonly BusinessCalendar _calendar;

    public GetCurrentUserQueryHandler(
        IUserQueries userQueries, ICurrentUserService currentUser, BusinessCalendar calendar)
    {
        _userQueries = userQueries;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        // The same month boundary the quota check uses, so /me shows what the next
        // AI call will be measured against (ADR-39, ADR-40)
        var monthStartUtc = _calendar.MonthStartUtc(DateTime.UtcNow);

        // A valid token for a user with no row: rare, but an honest 404 rather than a 500
        return await _userQueries.GetCurrentAsync(userId, monthStartUtc, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");
    }
}
