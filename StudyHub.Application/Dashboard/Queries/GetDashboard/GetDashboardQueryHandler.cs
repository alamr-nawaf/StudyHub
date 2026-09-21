using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Time;

namespace StudyHub.Application.Dashboard.Queries.GetDashboard;

/// <summary>
/// Returns the current user's dashboard (UC-08). The numbers of §15.4 live here, not in
/// Infrastructure: the handler reads the clock once, works out the urgent window and the
/// two caps, and hands them down as criteria (ADR-42).
/// </summary>
public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    /// <summary>Today and the three days after it count as urgent (§15.4).</summary>
    public const int UrgentWindowDays = 3;

    /// <summary>The urgent-task preview is capped, never paginated (§15.4).</summary>
    public const int MaxUrgentTasks = 10;

    /// <summary>The recent-course preview is capped, never paginated (§15.4).</summary>
    public const int MaxRecentCourses = 5;

    private readonly IDashboardQueries _dashboardQueries;
    private readonly ICurrentUserService _currentUser;
    private readonly BusinessCalendar _calendar;

    public GetDashboardQueryHandler(
        IDashboardQueries dashboardQueries, ICurrentUserService currentUser, BusinessCalendar calendar)
    {
        _dashboardQueries = dashboardQueries;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        // Read once: the overdue count, every isOverdue flag and the urgent window must
        // agree, and the response returns this instant as generatedAt (§15.4).
        // DateTime.UtcNow, not Now: Npgsql refuses a timestamptz parameter whose Kind is
        // not Utc (§14.1)
        var utcNow = DateTime.UtcNow;

        // Urgency is counted in Riyadh calendar days (ADR-40): from the start of today in
        // Riyadh, three whole days later ends at the next Riyadh midnight, so the window is
        // an exclusive bound four days out
        var urgentUntil = _calendar.DayStartUtc(utcNow).AddDays(UrgentWindowDays + 1);

        var criteria = new DashboardCriteria(utcNow, urgentUntil, MaxUrgentTasks, MaxRecentCourses);

        // A valid token whose account row is gone: 404, the same answer as GET /api/auth/me
        return await _dashboardQueries.GetAsync(userId, criteria, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");
    }
}
