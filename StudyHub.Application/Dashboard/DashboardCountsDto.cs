namespace StudyHub.Application.Dashboard;

/// <summary>
/// The dashboard's counts, all taken from one statement so that the task statuses always
/// add up to the task total (§15.4, ADR-42).
/// </summary>
public sealed record DashboardCountsDto(
    int Courses,
    int Notes,
    int Tasks,
    int PendingTasks,
    int InProgressTasks,
    int CompletedTasks,
    int OverdueTasks);
