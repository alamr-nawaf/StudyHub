using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Items.Commands.DeleteItem;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Items.Commands.DeleteItem;

public class DeleteItemCommandHandlerTests
{
    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeleteItemCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public DeleteItemCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new DeleteItemCommandHandler(
            _itemRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldMarkTheWholeSubtreeAcrossThreeLevels()
    {
        // Arrange
        var level0 = Note.Create(_currentUserId, "Root");
        var level1 = TaskItem.Create(_currentUserId, "Child", parent: level0);
        var level2 = Note.Create(_currentUserId, "Grandchild", parent: level1);

        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(level0.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(level0);

        _itemRepositoryMock
            .Setup(r => r.GetSubtreeAsync(level0.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Item> { level0, level1, level2 });

        // Act
        await _handler.Handle(new DeleteItemCommand(level0.Id), CancellationToken.None);

        // Assert
        level0.IsDeleted.Should().BeTrue();
        level1.IsDeleted.Should().BeTrue();
        level2.IsDeleted.Should().BeTrue();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMissingItem_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);

        // Act
        var act = async () => await _handler.Handle(
            new DeleteItemCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OnItemOfAnotherUser_ShouldThrowForbiddenAndNotTouchTheSubtree()
    {
        // Arrange
        var strangersNote = Note.Create(_strangerId, "Not mine");

        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(strangersNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strangersNote);

        // Act
        var act = async () => await _handler.Handle(
            new DeleteItemCommand(strangersNote.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        strangersNote.IsDeleted.Should().BeFalse();

        _itemRepositoryMock.Verify(
            r => r.GetSubtreeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}