using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Courses.Commands.DeleteCourse;

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

        // لا حاجة للمرور على المستويات: وراثة CourseId تجلبها كلها دفعة واحدة
        var items = await _itemRepository.GetByCourseAsync(course.Id, cancellationToken);

        foreach (var item in items)
            item.MarkAsDeleted();

        course.MarkAsDeleted();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}