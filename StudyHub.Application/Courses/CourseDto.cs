namespace StudyHub.Application.Courses;

/// <summary>
/// The shape of a course returned by the API.
/// </summary>
public record CourseDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
