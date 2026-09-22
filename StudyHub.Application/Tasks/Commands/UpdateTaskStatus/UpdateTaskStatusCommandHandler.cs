using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskStatus;

/// <summary>
/// Updates the status of one of the current user's tasks.
/// </summary>
public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand>
{
    private readonly IItemRepository _itemRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTaskStatusCommandHandler(
        IItemRepository itemRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _itemRepository = itemRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        // A note's id on a task's route names no task, so it is 404 and not 400
        if (await _itemRepository.GetByIdAsync(request.Id, cancellationToken) is not TaskItem task)
            throw new NotFoundException($"Task '{request.Id}' was not found.");

        if (task.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this task.");

        // ! because the validator refused null before this point: a handler may assume its
        // input is already well formed
        task.UpdateStatus(request.Status!.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
