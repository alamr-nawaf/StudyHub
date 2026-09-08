using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly StudyHubDbContext _context;

    public ItemRepository(StudyHubDbContext context) => _context = context;

    // Items لا Notes: الأب قد يكون ملاحظة أو مهمة، ولا نعرف أيهما قبل الجلب
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

        // خمس دورات كحد أقصى، لأن العمق مقيَّد بـ Item.MaxDepth
        while (currentLevel.Count > 0)
        {
            var children = await _context.Items
                .Where(i => i.ParentItemId != null && currentLevel.Contains(i.ParentItemId.Value))
                .ToListAsync(cancellationToken);

            if (children.Count == 0)
                break;

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