using StudyHub.Domain.Entities;

namespace StudyHub.Application.Courses;

/// <summary>
/// Maps courses to <see cref="CourseDto"/> (ADR-17).
/// </summary>
public static class CourseMappings
{
    // على IQueryable لا على الكيان: الإسقاط يصير SELECT للأعمدة المطلوبة، ولا يُحمَّل كيان أبدًا (§6)
    public static IQueryable<CourseDto> ToDto(this IQueryable<Course> courses) =>
        courses.Select(c => new CourseDto(
            c.Id,
            c.Title,
            c.Description,
            c.CreatedAt,
            c.UpdatedAt));
}
