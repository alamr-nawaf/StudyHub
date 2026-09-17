using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Courses;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Read-side access to courses: returns DTOs, never entities (ADR-32).
/// </summary>
public interface ICourseQueries
{
    // null حين لا يوجد الكورس أو حُذف منطقيًا — يكفي للتفريق بين 404 و403 دون جلب الكورس كله
    Task<Guid?> GetOwnerIdAsync(Guid courseId, CancellationToken cancellationToken);

    Task<PagedResult<CourseDto>> GetPageAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
}
