using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Items.Commands.DeleteItem;

/// <summary>
/// Soft-deletes one of the current user's items together with everything below it.
/// </summary>
public class DeleteItemCommandHandler : IRequestHandler<DeleteItemCommand>
{
    private readonly IItemRepository _itemRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteItemCommandHandler(
        IItemRepository itemRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _itemRepository = itemRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _itemRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Item '{request.Id}' was not found.");

        // Checking the root alone is enough: the entity enforces that a child's owner is its
        // parent's owner (rule 3.2.6)
        if (item.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this item.");

        var subtree = await _itemRepository.GetSubtreeAsync(item.Id, cancellationToken);

        foreach (var node in subtree)
            node.MarkAsDeleted();

        // One save is one transaction: either the whole tree goes or none of it does
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}