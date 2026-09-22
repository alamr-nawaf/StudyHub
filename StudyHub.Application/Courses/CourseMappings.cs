using StudyHub.Domain.Entities;

namespace StudyHub.Application.Courses;

/// <summary>
/// Maps courses to <see cref="CourseDto"/> (ADR-17).
/// </summary>
public static class CourseMappings
{
    // On IQueryable, not on the entity: the projection becomes a SELECT of exactly these
    // columns, and no entity is ever materialized (§6)
    public static IQueryable<CourseDto> ToDto(this IQueryable<Course> courses) =>
        courses.Select(c => new CourseDto(
            c.Id,
            c.Title,
            c.Description,
            c.CreatedAt,
            c.UpdatedAt));
}
