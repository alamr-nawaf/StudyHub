using MediatR;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Pagination;

namespace StudyHub.Application.Courses.Queries.GetCourses;

/// <summary>
/// Returns one page of the current user's courses.
/// </summary>
public class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, PagedResult<CourseDto>>
{
    private readonly ICourseQueries _courseQueries;
    private readonly ICurrentUserService _currentUser;

    public GetCoursesQueryHandler(ICourseQueries courseQueries, ICurrentUserService currentUser)
    {
        _courseQueries = courseQueries;
        _currentUser = currentUser;
    }

    public Task<PagedResult<CourseDto>> Handle(GetCoursesQuery request, CancellationToken cancellationToken) =>
        // المستخدم من التوكن لا من الطلب: القائمة لا يمكن أن تُطلب باسم غيرك
        _courseQueries.GetPageAsync(_currentUser.UserId, request.Page, request.PageSize, cancellationToken);
}
