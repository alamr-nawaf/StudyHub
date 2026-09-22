
namespace StudyHub.Domain.Enums;

/// <summary>
/// How urgent a task is, as the user judges it. It orders nothing by itself: the dashboard's
/// urgent list is ordered by due date (§15.4).
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}
