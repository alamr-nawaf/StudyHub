using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Items;
using StudyHub.Application.Items.Queries.GetRootItems;

namespace StudyHub.Application.Tests.Items.Queries.GetRootItems;

public class GetRootItemsQueryHandlerTests
{
    private readonly Mock<IItemQueries> _itemQueriesMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly GetRootItemsQueryHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetRootItemsQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetRootItemsQueryHandler(_itemQueriesMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_WithRootItems_ShouldReturnThePageFromTheQueries()
    {
        // Arrange
        var item = new ItemDto(Guid.NewGuid(), null, null, 0, ItemMappings.NoteKind, "Standalone", null,
            null, null, null, DateTime.UtcNow, null);
        var page = new PagedResult<ItemDto>(new[] { item }, 1, 20, 1);

        _itemQueriesMock
            .Setup(q => q.GetRootPageAsync(_currentUserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        // Act
        var result = await _handler.Handle(new GetRootItemsQuery(1, 20), CancellationToken.None);

        // Assert
        result.Should().BeSameAs(page);
    }

    [Fact]
    public async Task Handle_Always_ShouldAskForTheCurrentUsersItemsOnly()
    {
        // Arrange
        _itemQueriesMock
            .Setup(q => q.GetRootPageAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ItemDto>(Array.Empty<ItemDto>(), 3, 5, 0));

        // Act
        await _handler.Handle(new GetRootItemsQuery(3, 5), CancellationToken.None);

        // Assert
        _itemQueriesMock.Verify(
            q => q.GetRootPageAsync(_currentUserId, 3, 5, It.IsAny<CancellationToken>()),
            Times.Once);

        _itemQueriesMock.Verify(
            q => q.GetRootPageAsync(It.Is<Guid>(id => id != _currentUserId), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
