using MediatR;

namespace StudyHub.Application.Courses.Commands.DeleteCourse;

/// <summary>
/// Soft-deletes a course and everything under it.
/// </summary>
public record DeleteCourseCommand(Guid Id) : IRequest;