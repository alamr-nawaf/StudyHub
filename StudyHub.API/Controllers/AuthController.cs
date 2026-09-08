using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Users.Commands.RegisterUser;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await _mediator.Send(command, cancellationToken);
        return Created($"/api/users/{userId}", new { userId });
    }
}