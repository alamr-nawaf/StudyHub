using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Write-side access to notes and tasks: loads entities for commands to change (ADR-32).
/// </summary>
public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // The whole tree under a node, the node itself included
    Task<IReadOnlyList<Item>> GetSubtreeAsync(Guid rootId, CancellationToken cancellationToken);

    // Every item of a course at every depth, thanks to the inherited CourseId
    Task<IReadOnlyList<Item>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken);

    void Add(Item item);
}