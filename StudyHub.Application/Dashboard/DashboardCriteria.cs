namespace StudyHub.Application.Dashboard;

/// <summary>
/// The rules of §15.4, decided by the handler and handed to Infrastructure, which only
/// translates them into SQL (ADR-42). The instant is read once, so the overdue count, the
/// isOverdue flags and the urgent window all agree.
/// </summary>
public sealed record DashboardCriteria(
    DateTime UtcNow,
    DateTime UrgentUntil,
    int MaxUrgentTasks,
    int MaxRecentCourses);
