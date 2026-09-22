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

    // Items, not Notes or Tasks: the id may belong to either kind
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
            // Order before projecting: EF cannot see inside the DTO's constructor, so it
            // cannot translate an order written on it
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

        // One statement per level rather than WITH RECURSIVE: the depth is capped at five, and
        // the soft-delete filter keeps applying, which raw recursive SQL would bypass (ADR-11).
        // The bound counts from the root's own depth, not from zero: an item at depth 3 can
        // only have one level of children left
        for (var depth = root.Depth; depth < Item.MaxDepth && currentLevel.Count > 0; depth++)
        {
            var children = await _context.Items
                .Where(i => i.ParentItemId != null && currentLevel.Contains(i.ParentItemId.Value))
                .OrderBy(i => i.CreatedAt)
                .ThenBy(i => i.Id)
                .ToDto()
                .ToListAsync(cancellationToken);

            // The levels are appended in order, so the list comes out sorted by depth with no
            // in-memory sort
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
