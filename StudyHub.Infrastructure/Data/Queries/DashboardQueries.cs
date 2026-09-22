using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Dashboard;
using StudyHub.Domain.Enums;

namespace StudyHub.Infrastructure.Data.Queries;

/// <summary>
/// EF Core implementation of <see cref="IDashboardQueries"/>. It decides nothing: the rules
/// of §15.4 arrive as criteria and this class only translates them into three statements —
/// every count in one row, the urgent tasks, the recent courses (ADR-42). LINQ throughout,
/// so the soft-delete query filters apply by themselves.
/// </summary>
public class DashboardQueries : IDashboardQueries
{
    private readonly StudyHubDbContext _context;

    public DashboardQueries(StudyHubDbContext context) => _context = context;

    public async Task<DashboardDto?> GetAsync(
        Guid userId, DashboardCriteria criteria, CancellationToken cancellationToken)
    {
        // Copied into locals: EF translates a captured local, and a property access on the
        // criteria record inside a query tree is one more thing it has to understand
        var utcNow = criteria.UtcNow;
        var urgentUntil = criteria.UrgentUntil;
        var maxUrgentTasks = criteria.MaxUrgentTasks;
        var maxRecentCourses = criteria.MaxRecentCourses;

        // 1. Every count in one statement, anchored on the user's own row: one snapshot, so
        // the three task statuses always add up to the task total
        var counts = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                Courses = _context.Courses.Count(c => c.UserId == u.Id),
                Notes = _context.Notes.Count(n => n.UserId == u.Id),
                Pending = _context.Tasks.Count(t => t.UserId == u.Id && t.Status == StudyTaskStatus.Pending),
                InProgress = _context.Tasks.Count(t => t.UserId == u.Id && t.Status == StudyTaskStatus.InProgress),
                Completed = _context.Tasks.Count(t => t.UserId == u.Id && t.Status == StudyTaskStatus.Completed),
                Overdue = _context.Tasks.Count(t => t.UserId == u.Id
                    && t.Status != StudyTaskStatus.Completed
                    && t.DueDate < utcNow)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // No row means no account: the two list statements are never run
        if (counts is null)
            return null;

        // 2. The urgent tasks. Ordered on the entity and only then projected: EF cannot
        // translate an order on a DTO built through its constructor (M7)
        var urgentTasks = await _context.Tasks
            .Where(t => t.UserId == userId
                && t.Status != StudyTaskStatus.Completed
                && t.DueDate != null
                && t.DueDate < urgentUntil)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .Take(maxUrgentTasks)
            .Select(t => new UrgentTaskDto(
                t.Id, t.Title, t.CourseId, t.Status, t.Priority,
                t.DueDate!.Value, t.DueDate < utcNow))
            .ToListAsync(cancellationToken);

        // 3. The recent courses, by latest activity (ADR-41). The order is never taken from
        // UpdatedAt alone: PostgreSQL puts NULL first in a descending order, so a course
        // that was never edited would jump to the top of the list
        var recentCourses = await _context.Courses
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                c.Id,
                c.Title,
                CourseChangedAt = c.UpdatedAt ?? c.CreatedAt,
                // Every item of the course at any depth, because a child inherits its
                // root's CourseId (ADR-09); deleted items are filtered out by the query
                // filter, so a delete is not activity
                LastItemChangedAt = _context.Items
                    .Where(i => i.CourseId == c.Id)
                    .Max(i => (DateTime?)(i.UpdatedAt ?? i.CreatedAt))
            })
            .Select(x => new
            {
                x.Id,
                x.Title,
                LastActivityAt = x.LastItemChangedAt != null && x.LastItemChangedAt > x.CourseChangedAt
                    ? x.LastItemChangedAt.Value
                    : x.CourseChangedAt
            })
            .OrderByDescending(x => x.LastActivityAt)
            .ThenBy(x => x.Id)
            .Take(maxRecentCourses)
            .Select(x => new RecentCourseDto(x.Id, x.Title, x.LastActivityAt))
            .ToListAsync(cancellationToken);

        return new DashboardDto(
            utcNow,
            new DashboardCountsDto(
                counts.Courses,
                counts.Notes,
                // CK_Item_TaskFields and CK_Item_StatusValue guarantee that every task has
                // exactly one of the three statuses, so the three add up to the total
                counts.Pending + counts.InProgress + counts.Completed,
                counts.Pending,
                counts.InProgress,
                counts.Completed,
                counts.Overdue),
            urgentTasks,
            recentCourses);
    }
}
