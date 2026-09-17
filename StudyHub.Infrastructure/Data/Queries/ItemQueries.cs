using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Items;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Queries;

/// <summary>
/// EF Core implementation of <see cref="IItemQueries"/>; every read projects with Select.
/// </summary>
public class ItemQueries : IItemQueries
{
    private readonly StudyHubDbContext _context;

    public ItemQueries(StudyHubDbContext context) => _context = context;

    // Items لا Notes ولا Tasks: المعرّف قد يخص أيًّا منهما
    public Task<Guid?> GetOwnerIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        _context.Items
            .Where(i => i.Id == itemId)
            .Select(i => (Guid?)i.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ItemDto?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        _context.Items
            .Where(i => i.Id == itemId)
            .ToDto()
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PagedResult<ItemDto>> GetRootPageAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken) =>
        _context.Items
            .Where(i => i.UserId == userId && i.ParentItemId == null && i.CourseId == null)
            // الترتيب قبل الإسقاط: EF لا يرى ما داخل مُنشئ الـ DTO، فلا يترجم ترتيبًا عليه
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToDto()
            .ToPagedResultAsync(page, pageSize, cancellationToken);

    public async Task<IReadOnlyList<ItemDto>> GetSubtreeAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var root = await GetByIdAsync(itemId, cancellationToken);

        if (root is null)
            return Array.Empty<ItemDto>();

        var subtree = new List<ItemDto> { root };
        var currentLevel = new List<Guid> { root.Id };

        // جملة لكل مستوى لا WITH RECURSIVE: العمق محدود بخمسة، ومرشّح الحذف يبقى مطبَّقًا (ADR-11).
        // الحد من عمق الجذر لا من الصفر: عنصر في العمق 3 لا يملك إلا مستوى أبناء واحدًا
        for (var depth = root.Depth; depth < Item.MaxDepth && currentLevel.Count > 0; depth++)
        {
            var children = await _context.Items
                .Where(i => i.ParentItemId != null && currentLevel.Contains(i.ParentItemId.Value))
                .OrderBy(i => i.CreatedAt)
                .ThenBy(i => i.Id)
                .ToDto()
                .ToListAsync(cancellationToken);

            // المستويات تُضاف بالترتيب، فالقائمة مرتّبة بالعمق دون فرز في الذاكرة
            subtree.AddRange(children);
            currentLevel = children.Select(c => c.Id).ToList();
        }

        return subtree;
    }

    public async Task<IReadOnlyList<ItemDto>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        await _context.Items
            .Where(i => i.CourseId == courseId)
            .OrderBy(i => i.Depth)
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToDto()
            .ToListAsync(cancellationToken);
}
