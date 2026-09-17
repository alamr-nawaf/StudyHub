using MediatR;

namespace StudyHub.Application.Courses.Commands.UpdateCourse;

/// <summary>
/// Replaces a course's title and description; a null description clears it.
/// </summary>
// Id يأتي من المسار دائمًا؛ المتحكّم يكتب فوق أي قيمة وصلت في الجسم
public record UpdateCourseCommand(Guid Id, string Title, string? Description) : IRequest;
