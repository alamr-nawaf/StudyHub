using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Pagination;

namespace StudyHub.Infrastructure.Data.Queries;

/// <summary>
/// Turns an ordered, projected query into one page of results.
/// </summary>
internal static class PagedQueryExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> orderedQuery,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        // long rather than int: a huge page number overflows the multiplication, which turns
        // the offset negative and the response into a 500. A page past the end is not an error:
        // it is a 200 with an empty list, and no second round trip to the database
        var skip = (long)(page - 1) * pageSize;
        if (skip >= totalCount)
            return new PagedResult<T>(Array.Empty<T>(), page, pageSize, totalCount);

        var items = await orderedQuery
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }
}
