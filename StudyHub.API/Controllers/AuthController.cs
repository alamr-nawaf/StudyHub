using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Auth.Commands.Login;
using StudyHub.Application.Auth.Commands.Logout;
using StudyHub.Application.Auth.Commands.Refresh;
using StudyHub.Application.Auth.Queries.GetCurrentUser;
using StudyHub.Application.Users.Commands.RegisterUser;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await _mediator.Send(command, cancellationToken);
        return Created($"/api/users/{userId}", new { userId });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // مجهول عمدًا: توكن الوصول غالبًا منتهٍ حين يُحتاج التجديد (§8، الحاشية 1)
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // محمي بالسياسة الافتراضية: المعالِج يقارن مالك التوكن بـ sub (§9.3)
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // محمي بالسياسة الافتراضية. الاسم والبريد من القاعدة لا من التوكن، فالتوكن لا يحملهما (ADR-21)
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(user);
    }
}
