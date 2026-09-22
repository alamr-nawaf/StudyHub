using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="StudyHub.Application.Common.Interfaces.IItemRepository"/>.
/// </summary>
public class ItemRepository : IItemRepository
{
    private readonly StudyHubDbContext _context;

    public ItemRepository(StudyHubDbContext context) => _context = context;

    // Items, not Notes: a parent may be a note or a task, and which one is not known before
    // it is loaded
    public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Item>> GetSubtreeAsync(Guid rootId, CancellationToken cancellationToken)
    {
        var root = await _context.Items
            .FirstOrDefaultAsync(i => i.Id == rootId, cancellationToken);

        if (root is null)
            return Array.Empty<Item>();

        var subtree = new List<Item> { root };
        var currentLevel = new List<Guid> { root.Id };

        // A hard bound on the iterations: the depth is capped by Item.MaxDepth, and this
        // ceiling keeps the loop finite even if a direct UPDATE in the database ever created
        // a cycle
        for (var level = 0; level <= Item.MaxDepth && currentLevel.Count > 0; level++)
        {
            var children = await _context.Items
                .Where(i => i.ParentItemId != null && currentLevel.Contains(i.ParentItemId.Value))
                .ToListAsync(cancellationToken);

            

            subtree.AddRange(children);
            currentLevel = children.Select(c => c.Id).ToList();
        }

        return subtree;
    }

    public async Task<IReadOnlyList<Item>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken)
        => await _context.Items
            .Where(i => i.CourseId == courseId)
            .ToListAsync(cancellationToken);

    public void Add(Item item) => _context.Items.Add(item);
}