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

        // long لا int: رقم صفحة ضخم يفيض عند الضرب فيصير الإزاحة سالبة و500.
        // صفحة بعد النهاية ليست خطأ: 200 وقائمة فارغة، بلا جولة ثانية على القاعدة
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
