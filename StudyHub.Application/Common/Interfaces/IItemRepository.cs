using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // الشجرة كاملة تحت عقدة، شاملةً العقدة نفسها
    Task<IReadOnlyList<Item>> GetSubtreeAsync(Guid rootId, CancellationToken cancellationToken);

    // كل عناصر كورس عبر كل المستويات — بفضل وراثة CourseId
    Task<IReadOnlyList<Item>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken);

    void Add(Item item);
}