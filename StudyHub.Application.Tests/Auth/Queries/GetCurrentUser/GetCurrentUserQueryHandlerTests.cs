using FluentAssertions;
using Moq;
using StudyHub.Application.Auth.Queries.GetCurrentUser;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Auth.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IUserQueries> _userQueriesMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly GetCurrentUserQueryHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetCurrentUserQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetCurrentUserQueryHandler(_userQueriesMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingUser_ShouldReturnTheCurrentUsersProfile()
    {
        // Arrange
        var profile = new CurrentUserDto(_currentUserId, "Session User", "session@test.com", UserRole.User, 100_000, 0);

        _userQueriesMock
            .Setup(q => q.GetCurrentAsync(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.Should().Be(profile);
    }

    [Fact]
    public async Task Handle_MissingUser_ShouldThrowNotFound()
    {
        // Arrange
        _userQueriesMock
            .Setup(q => q.GetCurrentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentUserDto?)null);

        // Act
        var act = () => _handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
