using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Items;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Read-side access to notes and tasks: returns DTOs, never entities (ADR-32).
/// </summary>
public interface IItemQueries
{
    // null حين لا يوجد العنصر أو حُذف منطقيًا
    Task<Guid?> GetOwnerIdAsync(Guid itemId, CancellationToken cancellationToken);

    Task<ItemDto?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken);

    // العناصر المستقلة فقط: بلا أب وبلا كورس
    Task<PagedResult<ItemDto>> GetRootPageAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    // العنصر وكل أحفاده، قائمة مسطّحة مرتّبة بالعمق ثم وقت الإنشاء (ADR-23)
    Task<IReadOnlyList<ItemDto>> GetSubtreeAsync(Guid itemId, CancellationToken cancellationToken);

    // كل عناصر الكورس في كل المستويات — بفضل وراثة CourseId (ADR-09)
    Task<IReadOnlyList<ItemDto>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken);
}
