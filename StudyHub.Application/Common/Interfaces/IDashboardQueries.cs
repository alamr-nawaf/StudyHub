using StudyHub.Application.Dashboard;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Read-side access to the dashboard: one method, returning DTOs (ADR-32, ADR-42).
/// </summary>
public interface IDashboardQueries
{
    // null when no account row exists for the id, which the handler turns into a 404
    Task<DashboardDto?> GetAsync(Guid userId, DashboardCriteria criteria, CancellationToken cancellationToken);
}
