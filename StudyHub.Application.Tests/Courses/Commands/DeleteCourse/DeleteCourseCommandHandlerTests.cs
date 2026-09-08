using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Courses.Commands.DeleteCourse;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Courses.Commands.DeleteCourse;

public class DeleteCourseCommandHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeleteCourseCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public DeleteCourseCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new DeleteCourseCommandHandler(
            _courseRepositoryMock.Object,
            _itemRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldMarkCourseAndEveryItemThatBelongsToIt()
    {
        // Arrange
        var course = Course.Create(_currentUserId, "OOP 101");

        var rootNote = Note.Create(_currentUserId, "Root", courseId: course.Id);
        var childTask = TaskItem.Create(_currentUserId, "Child", parent: rootNote);

        _courseRepositoryMock
            .Setup(r => r.GetByIdAsync(course.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);

        _itemRepositoryMock
            .Setup(r => r.GetByCourseAsync(course.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Item> { rootNote, childTask });

        // Act
        await _handler.Handle(new DeleteCourseCommand(course.Id), CancellationToken.None);

        // Assert
        course.IsDeleted.Should().BeTrue();
        rootNote.IsDeleted.Should().BeTrue();
        childTask.IsDeleted.Should().BeTrue();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMissingCourse_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        _courseRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Course?)null);

        // Act
        var act = async () => await _handler.Handle(
            new DeleteCourseCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OnCourseOfAnotherUser_ShouldThrowForbiddenAndNotFetchItems()
    {
        // Arrange
        var strangersCourse = Course.Create(_strangerId, "Not my course");

        _courseRepositoryMock
            .Setup(r => r.GetByIdAsync(strangersCourse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strangersCourse);

        // Act
        var act = async () => await _handler.Handle(
            new DeleteCourseCommand(strangersCourse.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        strangersCourse.IsDeleted.Should().BeFalse();

        _itemRepositoryMock.Verify(
            r => r.GetByCourseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}