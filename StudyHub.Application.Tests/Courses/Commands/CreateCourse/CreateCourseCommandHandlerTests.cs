using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Courses.Commands.CreateCourse;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Courses.Commands.CreateCourse;

public class CreateCourseCommandHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateCourseCommandHandler _handler;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public CreateCourseCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new CreateCourseCommandHandler(
            _courseRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldAddCourseAndReturnId()
    {
        // Arrange
        var command = new CreateCourseCommand("OOP 101", "Object oriented programming");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();

        _courseRepositoryMock.Verify(r => r.Add(It.IsAny<Course>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAssignCourseToCurrentUserNotToAnyoneElse()
    {
        // Arrange
        var command = new CreateCourseCommand("OOP 101", null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _courseRepositoryMock.Verify(
            r => r.Add(It.Is<Course>(c => c.UserId == _currentUserId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyTitle_ShouldThrowAndSaveNothing()
    {
        // Arrange
        var command = new CreateCourseCommand("   ", null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        _courseRepositoryMock.Verify(r => r.Add(It.IsAny<Course>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}