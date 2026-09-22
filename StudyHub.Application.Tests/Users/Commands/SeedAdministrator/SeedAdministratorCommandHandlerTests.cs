using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Application.Users.Commands.SeedAdministrator;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Users.Commands.SeedAdministrator;

public class SeedAdministratorCommandHandlerTests
{
    private static readonly UserQuotaSettings QuotaSettings = new(DefaultMonthlyTokens: 100_000);

    private const string AdminEmail = "admin@test.com";

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly SeedAdministratorCommandHandler _handler;

    public SeedAdministratorCommandHandlerTests()
    {
        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        _handler = new SeedAdministratorCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _unitOfWorkMock.Object,
            QuotaSettings);
    }

    private void StoredUserIs(User? user) =>
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(AdminEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

    [Fact]
    public async Task Handle_NoAccount_ShouldCreateAdministratorInOneSave()
    {
        StoredUserIs(null);

        User? added = null;
        _userRepositoryMock.Setup(r => r.Add(It.IsAny<User>())).Callback<User>(u => added = u);

        var result = await _handler.Handle(
            new SeedAdministratorCommand(AdminEmail, "Admin", "Password123"), CancellationToken.None);

        added.Should().NotBeNull();
        added!.Role.Should().Be(UserRole.Admin);
        added.PasswordHash.Should().Be("hashed");

        result.Should().Be(new SeedAdministratorResult(added.Id, Created: true, IsActive: true));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingAccount_ShouldPromoteWithoutTouchingPassword()
    {
        var existing = User.Create("Existing", AdminEmail, "original-hash", 100_000);
        StoredUserIs(existing);

        var result = await _handler.Handle(
            new SeedAdministratorCommand(AdminEmail, "Admin", "NewPassword123"), CancellationToken.None);

        existing.Role.Should().Be(UserRole.Admin);
        existing.PasswordHash.Should().Be("original-hash");
        result.Created.Should().BeFalse();

        // Stale configuration must not reset a password its owner has since changed
        _passwordHasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingInactiveAccount_ShouldReportInactive()
    {
        var existing = User.Create("Existing", AdminEmail, "original-hash", 100_000);
        existing.Deactivate();
        StoredUserIs(existing);

        var result = await _handler.Handle(
            new SeedAdministratorCommand(AdminEmail, null, null), CancellationToken.None);

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoAccountAndNoPassword_ShouldThrowWithoutSaving()
    {
        StoredUserIs(null);

        var act = () => _handler.Handle(
            new SeedAdministratorCommand(AdminEmail, "Admin", null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
