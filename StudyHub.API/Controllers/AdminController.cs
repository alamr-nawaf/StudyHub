using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Users.Commands.DeactivateUser;
using StudyHub.Domain.Authorization;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/admin/users")]
/// <summary>
/// The administrative endpoints (UC-09), guarded by permission rather than by ownership.
/// </summary>
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = Permissions.UsersDeactivate)]
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeactivateUserCommand(id), cancellationToken);
        return NoContent();
    }
}
