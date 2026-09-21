using MediatR;

namespace StudyHub.Application.Dashboard.Queries.GetDashboard;

/// <summary>
/// Asks for the current user's dashboard. It carries nothing: the caller comes from the
/// token, and every number of §15.4 is the handler's (ADR-42).
/// </summary>
public record GetDashboardQuery : IRequest<DashboardDto>;
