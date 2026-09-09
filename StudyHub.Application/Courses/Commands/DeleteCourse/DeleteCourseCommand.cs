using MediatR;

namespace StudyHub.Application.Courses.Commands.DeleteCourse;

public record DeleteCourseCommand(Guid Id) : IRequest;