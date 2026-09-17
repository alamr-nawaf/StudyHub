using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Courses;
using StudyHub.Application.Courses.Queries.GetCourses;

namespace StudyHub.Application.Tests.Courses.Queries.GetCourses;

public class GetCoursesQueryHandlerTests
{
    private readonly Mock<ICourseQueries> _courseQueriesMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly GetCoursesQueryHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetCoursesQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetCoursesQueryHandler(_courseQueriesMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_WithCourses_ShouldReturnThePageFromTheQueries()
    {
        // Arrange
        var course = new CourseDto(Guid.NewGuid(), "OOP 101", null, DateTime.UtcNow, null);
        var page = new PagedResult<CourseDto>(new[] { course }, 2, 10, 11);

        _courseQueriesMock
            .Setup(q => q.GetPageAsync(_currentUserId, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        // Act
        var result = await _handler.Handle(new GetCoursesQuery(2, 10), CancellationToken.None);

        // Assert
        result.Should().BeSameAs(page);
    }

    [Fact]
    public async Task Handle_Always_ShouldAskForTheCurrentUsersCoursesOnly()
    {
        // Arrange
        _courseQueriesMock
            .Setup(q => q.GetPageAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CourseDto>(Array.Empty<CourseDto>(), 1, 20, 0));

        // Act
        await _handler.Handle(new GetCoursesQuery(1, 20), CancellationToken.None);

        // Assert
        _courseQueriesMock.Verify(
            q => q.GetPageAsync(_currentUserId, 1, 20, It.IsAny<CancellationToken>()),
            Times.Once);

        _courseQueriesMock.Verify(
            q => q.GetPageAsync(It.Is<Guid>(id => id != _currentUserId), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
