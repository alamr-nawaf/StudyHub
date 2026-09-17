using MediatR;
using StudyHub.Application.Common.Pagination;

namespace StudyHub.Application.Courses.Queries.GetCourses;

/// <summary>
/// Requests one page of the current user's courses.
/// </summary>
public record GetCoursesQuery(int Page, int PageSize) : IRequest<PagedResult<CourseDto>>;
