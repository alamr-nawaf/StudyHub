using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Users.Commands.RegisterUser;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Users.Commands.RegisterUser;

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _handler = new RegisterUserCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithNewEmail_ShouldCreateUserAndReturnId()
    {
        // Arrange
        var command = new RegisterUserCommand("Ahmed Ali", "ahmed@test.com", "Password123", "Password123");

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(h => h.Hash(command.Password))
            .Returns("hashed-password");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();

        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldThrowConflictExceptionAndNotSaveAnything()
    {
        // Arrange
        var command = new RegisterUserCommand("Ahmed Ali", "ahmed@test.com", "Password123", "Password123");

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();

        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Never);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldNotLeakTheEmailInTheMessage()
    {
        // Arrange
        var command = new RegisterUserCommand("Ahmed Ali", "ahmed@test.com", "Password123", "Password123");

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().NotContain(command.Email);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldHashPasswordBeforeStoringUser()
    {
        // Arrange
        var command = new RegisterUserCommand("Sara", "sara@test.com", "Password123", "Password123");

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(h => h.Hash(command.Password))
            .Returns("super-secret-hash");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _passwordHasherMock.Verify(h => h.Hash(command.Password), Times.Once);

        _userRepositoryMock.Verify(
            r => r.Add(It.Is<User>(u => u.PasswordHash == "super-secret-hash")),
            Times.Once);
    }
}