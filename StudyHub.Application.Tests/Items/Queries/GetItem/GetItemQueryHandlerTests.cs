using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Items;
using StudyHub.Application.Items.Queries.GetItem;

namespace StudyHub.Application.Tests.Items.Queries.GetItem;

public class GetItemQueryHandlerTests
{
    private readonly Mock<IItemQueries> _itemQueriesMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly GetItemQueryHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetItemQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetItemQueryHandler(_itemQueriesMock.Object, _currentUserMock.Object);
    }

    private static ItemDto NoteDto(Guid id) =>
        new(id, null, null, 0, ItemMappings.NoteKind, "Lecture notes", null, null, null, null, DateTime.UtcNow, null);

    [Fact]
    public async Task Handle_OwnItem_ShouldReturnIt()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var dto = NoteDto(itemId);

        _itemQueriesMock
            .Setup(q => q.GetOwnerIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currentUserId);

        _itemQueriesMock
            .Setup(q => q.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _handler.Handle(new GetItemQuery(itemId), CancellationToken.None);

        // Assert
        result.Should().Be(dto);
    }

    [Fact]
    public async Task Handle_MissingItem_ShouldThrowNotFound()
    {
        // Arrange
        _itemQueriesMock
            .Setup(q => q.GetOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        // Act
        var act = () => _handler.Handle(new GetItemQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _itemQueriesMock.Verify(
            q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ItemOfAnotherUser_ShouldThrowForbiddenAndNotFetchIt()
    {
        // Arrange
        var itemId = Guid.NewGuid();

        _itemQueriesMock
            .Setup(q => q.GetOwnerIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(new GetItemQuery(itemId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _itemQueriesMock.Verify(
            q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
