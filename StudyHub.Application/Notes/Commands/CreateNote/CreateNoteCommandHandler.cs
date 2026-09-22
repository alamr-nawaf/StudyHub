using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Notes.Commands.CreateNote;

/// <summary>
/// Creates a note, under a parent item or a course or neither, after checking that whatever
/// it is attached to belongs to the caller and can still take a child.
/// </summary>
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

            // 403, not 404: the message tells "does not exist" apart from "not yours"
            if (parent.UserId != userId)
                throw new ForbiddenException("You do not own the parent item.");
            if (parent.IsAtMaxDepth)
                throw new ConflictException("The parent item is at the maximum depth.");
        }
        else if (request.CourseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(request.CourseId.Value, cancellationToken)
                ?? throw new NotFoundException($"Course '{request.CourseId}' was not found.");

            if (course.UserId != userId)
                throw new ForbiddenException("You do not own this course.");
        }


        // The entity checks ownership, depth and deletion again: the last layer, and the
        // only one that cannot be forgotten by a caller
        var note = Note.Create(userId, request.Title, request.Content, parent, request.CourseId);

        _itemRepository.Add(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return note.Id;
    }
}