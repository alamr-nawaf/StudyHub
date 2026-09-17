using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Items;
using StudyHub.Application.Items.Queries.GetItemTree;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Items.Queries.GetItemTree;

public class GetItemTreeQueryHandlerTests
{
    private readonly Mock<IItemQueries> _itemQueriesMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly GetItemTreeQueryHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetItemTreeQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetItemTreeQueryHandler(_itemQueriesMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_OwnItem_ShouldReturnItsSubtree()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var tree = new List<ItemDto>
        {
            new(rootId, null, null, 0, ItemMappings.NoteKind, "Root", null, null, null, null, DateTime.UtcNow, null),
            new(Guid.NewGuid(), rootId, null, 1, ItemMappings.TaskKind, "Child", null,
                StudyTaskStatus.Pending, TaskPriority.Medium, null, DateTime.UtcNow, null)
        };

        _itemQueriesMock
            .Setup(q => q.GetOwnerIdAsync(rootId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currentUserId);

        _itemQueriesMock
            .Setup(q => q.GetSubtreeAsync(rootId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tree);

        // Act
        var result = await _handler.Handle(new GetItemTreeQuery(rootId), CancellationToken.None);

        // Assert
        result.Should().BeSameAs(tree);
    }

    [Fact]
    public async Task Handle_MissingItem_ShouldThrowNotFound()
    {
        // Arrange
        _itemQueriesMock
            .Setup(q => q.GetOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        // Act
        var act = () => _handler.Handle(new GetItemTreeQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _itemQueriesMock.Verify(
            q => q.GetSubtreeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ItemOfAnotherUser_ShouldThrowForbiddenAndNotFetchTheTree()
    {
        // Arrange
        var itemId = Guid.NewGuid();

        _itemQueriesMock
            .Setup(q => q.GetOwnerIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(new GetItemTreeQuery(itemId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _itemQueriesMock.Verify(
            q => q.GetSubtreeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
