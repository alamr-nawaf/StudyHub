using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Items.Queries.GetItemTree;

/// <summary>
/// Returns one of the current user's items and its whole subtree, never paginated (Requirements §14.2).
/// </summary>
public class GetItemTreeQueryHandler : IRequestHandler<GetItemTreeQuery, IReadOnlyList<ItemDto>>
{
    private readonly IItemQueries _itemQueries;
    private readonly ICurrentUserService _currentUser;

    public GetItemTreeQueryHandler(IItemQueries itemQueries, ICurrentUserService currentUser)
    {
        _itemQueries = itemQueries;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ItemDto>> Handle(GetItemTreeQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await _itemQueries.GetOwnerIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Item '{request.Id}' was not found.");

        // Children inherit their parent's owner (rule 3.2.6), so checking the root alone
        // covers the whole tree
        if (ownerId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this item.");

        return await _itemQueries.GetSubtreeAsync(request.Id, cancellationToken);
    }
}
