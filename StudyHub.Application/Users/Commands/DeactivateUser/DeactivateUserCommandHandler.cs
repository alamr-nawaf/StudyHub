using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Users.Commands.DeactivateUser;

/// <summary>
/// UC-09. There is no ownership check: this is the first handler that works on somebody
/// else's row, and what guards it is the role permission on the endpoint. It revokes no
/// refresh token either — refresh already refuses a deactivated account (§9.3), and an
/// access token simply lives out its lifetime (§9.4).
/// </summary>
public class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"User '{request.Id}' was not found.");

        // Tolerant: an already-inactive account passes without an error, so an administrator
        // does not have to check first
        user.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
