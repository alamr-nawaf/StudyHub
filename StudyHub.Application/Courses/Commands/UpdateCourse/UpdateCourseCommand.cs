using MediatR;

namespace StudyHub.Application.Courses.Commands.UpdateCourse;

/// <summary>
/// Replaces a course's title and description; a null description clears it.
/// </summary>
// The id always comes from the route: the controller overwrites whatever the body carried
public record UpdateCourseCommand(Guid Id, string Title, string? Description) : IRequest;
