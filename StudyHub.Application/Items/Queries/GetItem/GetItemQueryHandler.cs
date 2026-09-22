using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Items.Queries.GetItem;

/// <summary>
/// Returns one of the current user's notes or tasks.
/// </summary>
public class GetItemQueryHandler : IRequestHandler<GetItemQuery, ItemDto>
{
    private readonly IItemQueries _itemQueries;
    private readonly ICurrentUserService _currentUser;

    public GetItemQueryHandler(IItemQueries itemQueries, ICurrentUserService currentUser)
    {
        _itemQueries = itemQueries;
        _currentUser = currentUser;
    }

    public async Task<ItemDto> Handle(GetItemQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await _itemQueries.GetOwnerIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Item '{request.Id}' was not found.");

        // 403 as on the write path, and ownership is checked before any data is fetched (ADR-33)
        if (ownerId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this item.");

        // null here means it was deleted between the two statements: an honest 404, not a 500
        return await _itemQueries.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Item '{request.Id}' was not found.");
    }
}
