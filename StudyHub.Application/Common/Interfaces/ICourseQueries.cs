using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Courses;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Read-side access to courses: returns DTOs, never entities (ADR-32).
/// </summary>
public interface ICourseQueries
{
    // null when the course does not exist or was soft-deleted. The owner alone is enough to
    // tell a 404 from a 403 without loading the whole course
    Task<Guid?> GetOwnerIdAsync(Guid courseId, CancellationToken cancellationToken);

    Task<PagedResult<CourseDto>> GetPageAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
}
