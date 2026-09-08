using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Items.Commands.DeleteItem;

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

        // فحص الجذر وحده يكفي: الكيان يفرض أن مالك الابن هو مالك الأب
        if (item.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this item.");

        var subtree = await _itemRepository.GetSubtreeAsync(item.Id, cancellationToken);

        foreach (var node in subtree)
            node.MarkAsDeleted();

        // حفظ واحد = معاملة واحدة: إما الشجرة كاملة أو لا شيء منها
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}