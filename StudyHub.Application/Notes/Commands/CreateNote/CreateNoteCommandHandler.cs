using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Notes.Commands.CreateNote;

public class CreateNoteCommandHandler : IRequestHandler<CreateNoteCommand, Guid>
{
    private readonly IItemRepository _itemRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateNoteCommandHandler(
        IItemRepository itemRepository,
        ICourseRepository courseRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _itemRepository = itemRepository;
        _courseRepository = courseRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateNoteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        Item? parent = null;

        if (request.ParentItemId.HasValue)
        {
            parent = await _itemRepository.GetByIdAsync(request.ParentItemId.Value, cancellationToken)
                ?? throw new NotFoundException($"Parent item '{request.ParentItemId}' was not found.");

            // 403 لا 404: الرسالة تفرّق بين "غير موجود" و"ليس لك"
            if (parent.UserId != userId)
                throw new ForbiddenException("You do not own the parent item.");
        }
        else if (request.CourseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(request.CourseId.Value, cancellationToken)
                ?? throw new NotFoundException($"Course '{request.CourseId}' was not found.");

            if (course.UserId != userId)
                throw new ForbiddenException("You do not own this course.");
        }

        // الكيان يعيد فحص الملكية والعمق والحذف — الطبقة الأخيرة
        var note = Note.Create(userId, request.Title, request.Content, parent, request.CourseId);

        _itemRepository.Add(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return note.Id;
    }
}