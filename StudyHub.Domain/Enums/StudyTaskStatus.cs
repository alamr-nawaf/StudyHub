
namespace StudyHub.Domain.Enums;

/// <summary>
/// Where a task stands. Every task has exactly one of the three, which is what lets the
/// dashboard's per-status counts add up to the task total.
/// </summary>
public enum StudyTaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}
