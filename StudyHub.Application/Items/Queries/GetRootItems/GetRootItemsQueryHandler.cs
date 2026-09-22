using MediatR;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Pagination;

namespace StudyHub.Application.Items.Queries.GetRootItems;

/// <summary>
/// Returns one page of the current user's standalone items.
/// </summary>
public class GetRootItemsQueryHandler : IRequestHandler<GetRootItemsQuery, PagedResult<ItemDto>>
{
    private readonly IItemQueries _itemQueries;
    private readonly ICurrentUserService _currentUser;

    public GetRootItemsQueryHandler(IItemQueries itemQueries, ICurrentUserService currentUser)
    {
        _itemQueries = itemQueries;
        _currentUser = currentUser;
    }

    public Task<PagedResult<ItemDto>> Handle(GetRootItemsQuery request, CancellationToken cancellationToken) =>
        // The user comes from the token, not from the request: a list cannot be asked for in
        // somebody else's name
        _itemQueries.GetRootPageAsync(_currentUser.UserId, request.Page, request.PageSize, cancellationToken);
}
