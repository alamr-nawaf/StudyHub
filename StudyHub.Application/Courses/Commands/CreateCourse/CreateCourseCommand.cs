using MediatR;

namespace StudyHub.Application.Courses.Commands.CreateCourse;

/// <summary>
/// Creates a course for the caller. There is no UserId here on purpose: the owner comes from
/// the token through ICurrentUserService, never from the client.
/// </summary>
public record CreateCourseCommand(string Title, string? Description) : IRequest<Guid>;