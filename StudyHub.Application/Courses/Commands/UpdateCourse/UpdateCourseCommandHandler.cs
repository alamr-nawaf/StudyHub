using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Courses.Commands.UpdateCourse;

/// <summary>
/// Updates the details of one of the current user's courses.
/// </summary>
public class UpdateCourseCommandHandler : IRequestHandler<UpdateCourseCommand>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCourseCommandHandler(
        ICourseRepository courseRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Course '{request.Id}' was not found.");

        if (course.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this course.");

        course.UpdateDetails(request.Title, request.Description);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
