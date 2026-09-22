using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Courses.Queries.GetCourseTree;
using StudyHub.Application.Items;

namespace StudyHub.Application.Tests.Courses.Queries.GetCourseTree;

public class GetCourseTreeQueryHandlerTests
{
    private readonly Mock<ICourseQueries> _courseQueriesMock = new();
    private readonly Mock<IItemQueries> _itemQueriesMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly GetCourseTreeQueryHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetCourseTreeQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetCourseTreeQueryHandler(
            _courseQueriesMock.Object,
            _itemQueriesMock.Object,
            _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_OwnCourse_ShouldReturnEveryItemOfTheCourse()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var tree = new List<ItemDto>
        {
            new(Guid.NewGuid(), null, courseId, 0, ItemMappings.NoteKind, "Lecture 1", null,
                null, null, null, DateTime.UtcNow, null)
        };

        _courseQueriesMock
            .Setup(q => q.GetOwnerIdAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currentUserId);

        _itemQueriesMock
            .Setup(q => q.GetByCourseAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tree);

        // Act
        var result = await _handler.Handle(new GetCourseTreeQuery(courseId), CancellationToken.None);

        // Assert
        result.Should().BeSameAs(tree);
    }

    [Fact]
    public async Task Handle_MissingCourse_ShouldThrowNotFound()
    {
        // Arrange
        _courseQueriesMock
            .Setup(q => q.GetOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        // Act
        var act = () => _handler.Handle(new GetCourseTreeQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _itemQueriesMock.Verify(
            q => q.GetByCourseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CourseOfAnotherUser_ShouldThrowForbiddenAndNotFetchItems()
    {
        // Arrange
        var courseId = Guid.NewGuid();

        _courseQueriesMock
            .Setup(q => q.GetOwnerIdAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(new GetCourseTreeQuery(courseId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _itemQueriesMock.Verify(
            q => q.GetByCourseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
