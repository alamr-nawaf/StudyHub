using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Dashboard.Queries.GetDashboard;

namespace StudyHub.API.Controllers;

/// <summary>
/// The caller's dashboard (UC-08): counts, urgent tasks and recent courses, as §15.4
/// defines them. It carries no authorization attribute because the fallback policy already
/// requires an authenticated user.
/// </summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var dashboard = await _mediator.Send(new GetDashboardQuery(), cancellationToken);
        return Ok(dashboard);
    }
}
