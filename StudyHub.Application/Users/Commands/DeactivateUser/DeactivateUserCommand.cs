using MediatR;

namespace StudyHub.Application.Users.Commands.DeactivateUser;

/// <summary>
/// Deactivates one account (UC-09). The id is the target account, not the caller: the
/// permission was checked by the policy before the request reached this handler (§9.5).
/// </summary>
public record DeactivateUserCommand(Guid Id) : IRequest;
