using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Users.Commands.DeactivateUser;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Users.Commands.DeactivateUser;

public class DeactivateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeactivateUserCommandHandler _handler;

    public DeactivateUserCommandHandlerTests()
    {
        _handler = new DeactivateUserCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private User StoredUser()
    {
        var user = User.Create("Target", "target@test.com", "stored-hash", 100_000);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        return user;
    }

    [Fact]
    public async Task Handle_ActiveUser_ShouldDeactivateAndSave()
    {
        var user = StoredUser();

        await _handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        user.IsActive.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyInactiveUser_ShouldSucceed()
    {
        var user = StoredUser();
        user.Deactivate();

        var act = () => _handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        await act.Should().NotThrowAsync();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnknownUser_ShouldThrowNotFound()
    {
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _handler.Handle(new DeactivateUserCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
