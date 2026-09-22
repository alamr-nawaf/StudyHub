using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Tasks.Commands.UpdateTaskStatus;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Tasks.Commands.UpdateTaskStatus;

public class UpdateTaskStatusCommandHandlerTests
{
    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateTaskStatusCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public UpdateTaskStatusCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new UpdateTaskStatusCommandHandler(
            _itemRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    private void Store(Item item) =>
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

    [Fact]
    public async Task Handle_OwnTask_ShouldUpdateStatusAndSaveOnce()
    {
        // Arrange
        var task = TaskItem.Create(_currentUserId, "Review chapter 3");
        Store(task);

        // Act
        await _handler.Handle(
            new UpdateTaskStatusCommand(task.Id, StudyTaskStatus.Completed), CancellationToken.None);

        // Assert
        task.Status.Should().Be(StudyTaskStatus.Completed);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingItem_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);

        // Act
        var act = () => _handler.Handle(
            new UpdateTaskStatusCommand(Guid.NewGuid(), StudyTaskStatus.Completed), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoteId_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        var note = Note.Create(_currentUserId, "Just a note");
        Store(note);

        // Act
        var act = () => _handler.Handle(
            new UpdateTaskStatusCommand(note.Id, StudyTaskStatus.Completed), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TaskOfAnotherUser_ShouldThrowForbiddenAndChangeNothing()
    {
        // Arrange
        var strangersTask = TaskItem.Create(Guid.NewGuid(), "Not mine");
        Store(strangersTask);

        // Act
        var act = () => _handler.Handle(
            new UpdateTaskStatusCommand(strangersTask.Id, StudyTaskStatus.Completed), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        strangersTask.Status.Should().Be(StudyTaskStatus.Pending);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
