using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Tasks.Commands.CreateTask;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Tasks.Commands.CreateTask;

public class CreateTaskCommandHandlerTests
{
    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateTaskCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public CreateTaskCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new CreateTaskCommandHandler(
            _itemRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_AsRoot_ShouldCreateTaskAtDepthZeroWithPendingStatus()
    {
        // Arrange
        var command = new CreateTaskCommand("Review chapter 3", null, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();

        _itemRepositoryMock.Verify(
            r => r.Add(It.Is<TaskItem>(t =>
                t.Depth == 0 &&
                t.UserId == _currentUserId &&
                t.Status == StudyTaskStatus.Pending)),
            Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnderNoteParent_ShouldInheritCourseAndDepth()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var parentNote = Note.Create(_currentUserId, "Lecture notes", courseId: courseId);

        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(parentNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentNote);

        var command = new CreateTaskCommand("Extracted task", null, parentNote.Id, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _itemRepositoryMock.Verify(
            r => r.Add(It.Is<TaskItem>(t => t.Depth == 1 && t.CourseId == courseId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPriorityAndDueDate_ShouldStoreThem()
    {
        // Arrange
        var dueDate = DateTime.UtcNow.AddDays(7);
        var command = new CreateTaskCommand(
            "Urgent task", null, null, null, TaskPriority.High, dueDate);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _itemRepositoryMock.Verify(
            r => r.Add(It.Is<TaskItem>(t =>
                t.Priority == TaskPriority.High && t.DueDate == dueDate)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnderMissingParent_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);

        var command = new CreateTaskCommand("Orphan", null, Guid.NewGuid(), null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _itemRepositoryMock.Verify(r => r.Add(It.IsAny<Item>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnderParentOfAnotherUser_ShouldThrowForbiddenAndSaveNothing()
    {
        // Arrange
        var strangersNote = Note.Create(_strangerId, "Not mine");

        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(strangersNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strangersNote);

        var command = new CreateTaskCommand("Child", null, strangersNote.Id, null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _itemRepositoryMock.Verify(r => r.Add(It.IsAny<Item>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnderCourseOfAnotherUser_ShouldThrowForbidden()
    {
        // Arrange
        var strangersCourse = Course.Create(_strangerId, "Not my course");

        _courseRepositoryMock
            .Setup(r => r.GetByIdAsync(strangersCourse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strangersCourse);

        var command = new CreateTaskCommand("Task", null, null, strangersCourse.Id);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _itemRepositoryMock.Verify(r => r.Add(It.IsAny<Item>()), Times.Never);
    }
}