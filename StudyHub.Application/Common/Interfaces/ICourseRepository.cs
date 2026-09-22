using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Write-side access to courses: loads entities for commands to change (ADR-32).
/// </summary>
public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Synchronous on purpose: the id is generated in the entity, so adding one needs no
    // round trip to the database
    void Add(Course course);
}