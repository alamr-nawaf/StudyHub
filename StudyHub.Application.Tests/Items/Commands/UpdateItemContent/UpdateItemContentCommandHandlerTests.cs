using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Items.Commands.UpdateItemContent;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Items.Commands.UpdateItemContent;

public class UpdateItemContentCommandHandlerTests
{
    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateItemContentCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public UpdateItemContentCommandHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new UpdateItemContentCommandHandler(
            _itemRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);
    }

    private void Store(Item item) =>
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

    [Fact]
    public async Task Handle_OwnTask_ShouldReplaceContentAndKeepItATask()
    {
        // Arrange
        var task = TaskItem.Create(_currentUserId, "Old title", "Old content");
        Store(task);

        // Act
        await _handler.Handle(new UpdateItemContentCommand(task.Id, "New title", null), CancellationToken.None);

        // Assert
        task.Title.Should().Be("New title");
        task.Content.Should().BeNull();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingItem_ShouldThrowNotFoundAndSaveNothing()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);

        // Act
        var act = () => _handler.Handle(
            new UpdateItemContentCommand(Guid.NewGuid(), "Title", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ItemOfAnotherUser_ShouldThrowForbiddenAndChangeNothing()
    {
        // Arrange
        var strangersNote = Note.Create(Guid.NewGuid(), "Not mine");
        Store(strangersNote);

        // Act
        var act = () => _handler.Handle(
            new UpdateItemContentCommand(strangersNote.Id, "Hijacked", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        strangersNote.Title.Should().Be("Not mine");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
