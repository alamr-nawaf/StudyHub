using MediatR;

namespace StudyHub.Application.Courses.Commands.CreateCourse;

// لا UserId هنا — يأتي من ICurrentUserService لا من العميل
public record CreateCourseCommand(string Title, string? Description) : IRequest<Guid>;