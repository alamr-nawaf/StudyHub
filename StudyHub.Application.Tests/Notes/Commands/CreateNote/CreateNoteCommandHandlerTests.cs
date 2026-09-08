using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Notes.Commands.CreateNote;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Notes.Commands.CreateNote;

public class CreateNoteCommandHandlerTests
{
    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateNoteCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public CreateNoteCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new CreateNoteCommandHandler(
            _itemRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_AsRoot_ShouldCreateNoteAtDepthZero()
    {
        // Arrange
        var command = new CreateNoteCommand("Lecture 1", null, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();

        _itemRepositoryMock.Verify(
            r => r.Add(It.Is<Item>(i => i.Depth == 0 && i.UserId == _currentUserId)),
            Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnderExistingParent_ShouldInheritCourseAndDepth()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var parent = Note.Create(_currentUserId, "Parent", courseId: courseId);

        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var command = new CreateNoteCommand("Child", null, parent.Id, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _itemRepositoryMock.Verify(
            r => r.Add(It.Is<Item>(i => i.Depth == 1 && i.CourseId == courseId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnderMissingParent_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);

        var command = new CreateNoteCommand("Child", null, Guid.NewGuid(), null);

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

        var command = new CreateNoteCommand("Child", null, strangersNote.Id, null);

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

        var command = new CreateNoteCommand("Note", null, null, strangersCourse.Id);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _itemRepositoryMock.Verify(r => r.Add(It.IsAny<Item>()), Times.Never);
    }
}