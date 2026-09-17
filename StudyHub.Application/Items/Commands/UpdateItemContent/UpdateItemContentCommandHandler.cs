using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Items.Commands.UpdateItemContent;

/// <summary>
/// Updates the title and content of one of the current user's notes or tasks.
/// </summary>
public class UpdateItemContentCommandHandler : IRequestHandler<UpdateItemContentCommand>
{
    private readonly IItemRepository _itemRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateItemContentCommandHandler(
        IItemRepository itemRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _itemRepository = itemRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateItemContentCommand request, CancellationToken cancellationToken)
    {
        var item = await _itemRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Item '{request.Id}' was not found.");

        if (item.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this item.");

        item.UpdateContent(request.Title, request.Content);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
