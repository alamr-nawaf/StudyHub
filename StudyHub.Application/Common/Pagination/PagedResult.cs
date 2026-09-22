namespace StudyHub.Application.Common.Pagination;

/// <summary>
/// One page of a paginated collection, with the numbers a client needs to request the next page.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
