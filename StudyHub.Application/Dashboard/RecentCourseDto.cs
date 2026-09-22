namespace StudyHub.Application.Dashboard;

/// <summary>
/// One entry of the dashboard's recent-course preview, ordered by latest activity: the
/// later of the course's own last change and the newest change among its items (ADR-41).
/// </summary>
public sealed record RecentCourseDto(
    Guid Id,
    string Title,
    DateTime LastActivityAt);
