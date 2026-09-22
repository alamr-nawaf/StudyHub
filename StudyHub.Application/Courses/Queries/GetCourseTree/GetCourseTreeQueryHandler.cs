using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Items;

namespace StudyHub.Application.Courses.Queries.GetCourseTree;

/// <summary>
/// Returns the whole item tree of one of the current user's courses, never paginated (Requirements §14.2).
/// </summary>
public class GetCourseTreeQueryHandler : IRequestHandler<GetCourseTreeQuery, IReadOnlyList<ItemDto>>
{
    private readonly ICourseQueries _courseQueries;
    private readonly IItemQueries _itemQueries;
    private readonly ICurrentUserService _currentUser;

    public GetCourseTreeQueryHandler(
        ICourseQueries courseQueries,
        IItemQueries itemQueries,
        ICurrentUserService currentUser)
    {
        _courseQueries = courseQueries;
        _itemQueries = itemQueries;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ItemDto>> Handle(GetCourseTreeQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await _courseQueries.GetOwnerIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Course '{request.Id}' was not found.");

        if (ownerId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this course.");

        return await _itemQueries.GetByCourseAsync(request.Id, cancellationToken);
    }
}
