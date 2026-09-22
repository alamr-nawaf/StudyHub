using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Items;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Read-side access to notes and tasks: returns DTOs, never entities (ADR-32).
/// </summary>
public interface IItemQueries
{
    // null when the item does not exist or was soft-deleted
    Task<Guid?> GetOwnerIdAsync(Guid itemId, CancellationToken cancellationToken);

    Task<ItemDto?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken);

    // Standalone items only: no parent and no course
    Task<PagedResult<ItemDto>> GetRootPageAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    // The item and all its descendants, as a flat list ordered by depth then creation (ADR-23)
    Task<IReadOnlyList<ItemDto>> GetSubtreeAsync(Guid itemId, CancellationToken cancellationToken);

    // Every item of the course at every depth, thanks to the inherited CourseId (ADR-09)
    Task<IReadOnlyList<ItemDto>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken);
}
