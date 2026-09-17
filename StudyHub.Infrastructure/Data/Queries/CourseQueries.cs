using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Courses;

namespace StudyHub.Infrastructure.Data.Queries;

/// <summary>
/// EF Core implementation of <see cref="ICourseQueries"/>; every read projects with Select.
/// </summary>
public class CourseQueries : ICourseQueries
{
    private readonly StudyHubDbContext _context;

    public CourseQueries(StudyHubDbContext context) => _context = context;

    public Task<Guid?> GetOwnerIdAsync(Guid courseId, CancellationToken cancellationToken) =>
        _context.Courses
            .Where(c => c.Id == courseId)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PagedResult<CourseDto>> GetPageAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken) =>
        _context.Courses
            .Where(c => c.UserId == userId)
            // Id بعد CreatedAt: صفّان بالوقت نفسه يبقى ترتيبهما ثابتًا بين الصفحات
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToDto()
            .ToPagedResultAsync(page, pageSize, cancellationToken);
}
