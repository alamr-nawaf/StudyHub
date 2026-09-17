using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Courses.Commands.UpdateCourse;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Courses.Commands.UpdateCourse;

public class UpdateCourseCommandHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateCourseCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public UpdateCourseCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new UpdateCourseCommandHandler(
            _courseRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    private Course StoredCourse(Guid ownerId)
    {
        var course = Course.Create(ownerId, "OOP 101", "Old description");

        _courseRepositoryMock
            .Setup(r => r.GetByIdAsync(course.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);

        return course;
    }

    [Fact]
    public async Task Handle_OwnCourse_ShouldReplaceDetailsAndSaveOnce()
    {
        // Arrange
        var course = StoredCourse(_currentUserId);

        // Act
        await _handler.Handle(new UpdateCourseCommand(course.Id, "OOP 102", null), CancellationToken.None);

        // Assert
        course.Title.Should().Be("OOP 102");
        course.Description.Should().BeNull();
        course.UpdatedAt.Should().NotBeNull();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingCourse_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        _courseRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Course?)null);

        // Act
        var act = () => _handler.Handle(
            new UpdateCourseCommand(Guid.NewGuid(), "Title", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CourseOfAnotherUser_ShouldThrowForbiddenAndChangeNothing()
    {
        // Arrange
        var course = StoredCourse(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(
            new UpdateCourseCommand(course.Id, "Hijacked", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        course.Title.Should().Be("OOP 101");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
