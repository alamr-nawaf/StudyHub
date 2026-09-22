using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="StudyHub.Application.Common.Interfaces.ICourseRepository"/>.
/// </summary>
public class CourseRepository : ICourseRepository
{
    private readonly StudyHubDbContext _context;

    public CourseRepository(StudyHubDbContext context) => _context = context;

    public Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _context.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Course course) => _context.Courses.Add(course);
}