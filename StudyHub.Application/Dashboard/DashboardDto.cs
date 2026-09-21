namespace StudyHub.Application.Dashboard;

/// <summary>
/// What GET /api/dashboard returns for the caller (§15.4). GeneratedAt is the one instant
/// every number in the response was measured against.
/// </summary>
public sealed record DashboardDto(
    DateTime GeneratedAt,
    DashboardCountsDto Counts,
    IReadOnlyList<UrgentTaskDto> UrgentTasks,
    IReadOnlyList<RecentCourseDto> RecentCourses);
