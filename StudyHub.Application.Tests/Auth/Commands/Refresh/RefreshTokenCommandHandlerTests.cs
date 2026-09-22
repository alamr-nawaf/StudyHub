using FluentAssertions;
using Moq;
using StudyHub.Application.Auth.Commands.Refresh;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Auth.Commands.Refresh;

// Proves the order of §9.3: a token revoked by rotation triggers the mass revocation, while
// one revoked by logout and an expired one do not — and that the mass revocation does not
// depend on the save succeeding
public class RefreshTokenCommandHandlerTests
{
    private const string Raw = "raw-token";
    private const string Hash = "hashed-token";

    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RefreshTokenCommandHandler _handler;
    private readonly User _user = User.Create("Nawaf", "nawaf@test.com", "stored-hash", 100_000);

    public RefreshTokenCommandHandlerTests()
    {
        _tokens.Setup(t => t.HashRefreshToken(Raw)).Returns(Hash);

        _tokens.Setup(t => t.GenerateAccessToken(
                   It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<DateTime>()))
               .Returns((Guid _, UserRole _, DateTime now) => new AccessToken("access", now.AddMinutes(15)));

        _tokens.Setup(t => t.GenerateRefreshToken(It.IsAny<DateTime>()))
               .Returns((DateTime now) => new RefreshTokenResult("new-raw", "new-hash", now.AddDays(7)));

        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(_user);

        _handler = new RefreshTokenCommandHandler(
            _refreshTokens.Object, _users.Object, _tokens.Object, _unitOfWork.Object);
    }

    private void StoredIs(RefreshToken? token) =>
        _refreshTokens.Setup(r => r.GetByHashAsync(Hash, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(token);

    private RefreshToken ActiveToken() =>
        RefreshToken.Create(_user.Id, Hash, DateTime.UtcNow.AddDays(7));

    [Fact]
    public async Task Handle_ActiveToken_ShouldRotateAndLinkChain()
    {
        var old = ActiveToken();
        StoredIs(old);

        RefreshToken? added = null;
        _refreshTokens.Setup(r => r.Add(It.IsAny<RefreshToken>()))
                      .Callback<RefreshToken>(t => added = t);

        var result = await _handler.Handle(new RefreshTokenCommand(Raw), CancellationToken.None);

        result.RefreshToken.Should().Be("new-raw");
        result.ExpiresIn.Should().Be(900);

        added.Should().NotBeNull();
        added!.TokenHash.Should().Be("new-hash");

        // The old token is revoked and linked to the new one
        old.RevokedAt.Should().NotBeNull();
        old.ReplacedByTokenId.Should().Be(added.Id);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownToken_ShouldThrowInvalidCredentials()
    {
        StoredIs(null);

        var act = () => _handler.Handle(new RefreshTokenCommand(Raw), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExpiredToken_ShouldThrowInvalidCredentials()
    {
        StoredIs(RefreshToken.Create(_user.Id, Hash, DateTime.UtcNow.AddDays(-1)));

        var act = () => _handler.Handle(new RefreshTokenCommand(Raw), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();

        // Expiry is not a theft: no mass revocation
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RotatedToken_ShouldRevokeAllBeforeThrowing()
    {
        var revoked = ActiveToken();
        revoked.Revoke(DateTime.UtcNow, Guid.NewGuid());
        StoredIs(revoked);

        var revokeAllCalled = false;
        _refreshTokens.Setup(r => r.RevokeAllForUserAsync(
                          _user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                      .Callback(() => revokeAllCalled = true)
                      .Returns(Task.CompletedTask);

        var act = () => _handler.Handle(new RefreshTokenCommand(Raw), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();

        // It ran before the throw, not after it
        revokeAllCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LoggedOutToken_ShouldThrowWithoutRevokingAll()
    {
        var loggedOut = ActiveToken();
        loggedOut.Revoke(DateTime.UtcNow);
        StoredIs(loggedOut);

        var act = () => _handler.Handle(new RefreshTokenCommand(Raw), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();

        // A dead chain cannot be stolen from: a mass revocation here would turn any old token
        // into a sign-out-everywhere button (A23)
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RotatedToken_WithConcurrentLegitimateRotation_ShouldStillRevokeAll()
    {
        var revoked = ActiveToken();
        revoked.Revoke(DateTime.UtcNow, Guid.NewGuid());
        StoredIs(revoked);

        _refreshTokens.Setup(r => r.RevokeAllForUserAsync(
                          _user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        // Had the mass revocation gone through the unit of work, it would have been lost
        // entirely to a concurrency conflict on another row
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new ConflictException("concurrency"));

        var act = () => _handler.Handle(new RefreshTokenCommand(Raw), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();

        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(
            _user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InactiveUser_ShouldThrowInvalidCredentials()
    {
        StoredIs(ActiveToken());
        _user.Deactivate();

        var act = () => _handler.Handle(new RefreshTokenCommand(Raw), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}