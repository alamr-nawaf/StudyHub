using MediatR;
using StudyHub.Application.Common.Pagination;

namespace StudyHub.Application.Items.Queries.GetRootItems;

/// <summary>
/// Requests one page of the current user's standalone items: no parent and no course.
/// </summary>
public record GetRootItemsQuery(int Page, int PageSize) : IRequest<PagedResult<ItemDto>>;
