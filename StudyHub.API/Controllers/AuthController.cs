using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyHub.API.Common;
using StudyHub.Application.Auth.Commands.Login;
using StudyHub.Application.Auth.Commands.Logout;
using StudyHub.Application.Auth.Commands.Refresh;
using StudyHub.Application.Auth.Queries.GetCurrentUser;
using StudyHub.Application.Users.Commands.RegisterUser;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/auth")]
/// <summary>
/// Registration, the credential endpoints and the caller's own profile.
/// </summary>
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    // Credential endpoints are where enumeration and password guessing happen, and an
    // anonymous caller has no key but its address (ADR-45, §9.4)
    [EnableRateLimiting(RateLimitingSettings.AuthPolicy)]
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await _mediator.Send(command, cancellationToken);
        return Created($"/api/users/{userId}", new { userId });
    }

    [EnableRateLimiting(RateLimitingSettings.AuthPolicy)]
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // Anonymous by design: the access token it replaces has usually already expired, so
    // requiring one would make this endpoint useless exactly when it is needed (§8)
    [EnableRateLimiting(RateLimitingSettings.AuthPolicy)]
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // Protected by the fallback policy: the handler compares the token's owner with sub (§9.3)
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // Protected by the fallback policy. The name and e-mail come from the database, not from
    // the token, which deliberately does not carry them (ADR-21)
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(user);
    }
}
