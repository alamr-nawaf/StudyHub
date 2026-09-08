using MediatR;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Courses.Commands.CreateCourse;

public class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, Guid>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCourseCommandHandler(
        ICourseRepository courseRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        var course = Course.Create(_currentUser.UserId, request.Title, request.Description);

        _courseRepository.Add(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return course.Id;
    }
}