using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Courses.Commands.DeleteCourse;

/// <summary>
/// Soft-deletes one of the current user's courses together with its whole item tree, in one
/// save: either all of it goes or none of it does (ADR-06).
/// </summary>
public class DeleteCourseCommandHandler : IRequestHandler<DeleteCourseCommand>
{
    private readonly ICourseRepository _courseRepository;
    private readonly IItemRepository _itemRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCourseCommandHandler(
        ICourseRepository courseRepository,
        IItemRepository itemRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _itemRepository = itemRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Course '{request.Id}' was not found.");

        if (course.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this course.");

        // No walk down the levels is needed: the inherited CourseId brings every item of the
        // course back at once, whatever its depth (ADR-09)
        var items = await _itemRepository.GetByCourseAsync(course.Id, cancellationToken);

        foreach (var item in items)
            item.MarkAsDeleted();

        course.MarkAsDeleted();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}