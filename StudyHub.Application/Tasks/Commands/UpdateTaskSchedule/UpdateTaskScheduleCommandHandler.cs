using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskSchedule;

/// <summary>
/// Updates the priority and due date of one of the current user's tasks.
/// </summary>
public class UpdateTaskScheduleCommandHandler : IRequestHandler<UpdateTaskScheduleCommand>
{
    private readonly IItemRepository _itemRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTaskScheduleCommandHandler(
        IItemRepository itemRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _itemRepository = itemRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateTaskScheduleCommand request, CancellationToken cancellationToken)
    {
        // معرّف ملاحظة على مسار مهمة: لا توجد مهمة بهذا المعرّف، فهو 404 لا 400
        if (await _itemRepository.GetByIdAsync(request.Id, cancellationToken) is not TaskItem task)
            throw new NotFoundException($"Task '{request.Id}' was not found.");

        if (task.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this task.");

        // ! لأن المدقّق رفض null قبل الوصول هنا (المعالِج يفترض مدخلاته صحيحة)
        task.UpdateSchedule(request.Priority!.Value, request.DueDate);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
