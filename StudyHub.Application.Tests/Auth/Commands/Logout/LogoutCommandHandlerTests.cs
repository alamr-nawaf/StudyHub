using FluentAssertions;
using Moq;
using StudyHub.Application.Auth.Commands.Logout;
using StudyHub.Application.Common.Interfaces;
using DomainRefreshToken = StudyHub.Domain.Entities.RefreshToken;

namespace StudyHub.Application.Tests.Auth.Commands.Logout;

// يثبت أن الخروج صامت في كل الحالات، وأنه لا يشغّل كشف إعادة الاستخدام
public class LogoutCommandHandlerTests
{
    private const string Raw = "raw-token";
    private const string Hash = "hashed-token";

    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LogoutCommandHandler _handler;
    private readonly Guid _callerId = Guid.NewGuid();

    public LogoutCommandHandlerTests()
    {
        _tokens.Setup(t => t.HashRefreshToken(Raw)).Returns(Hash);
        _currentUser.Setup(c => c.UserId).Returns(_callerId);

        _handler = new LogoutCommandHandler(
            _refreshTokens.Object, _currentUser.Object, _tokens.Object, _unitOfWork.Object);
    }

    private void StoredIs(DomainRefreshToken token) =>
        _refreshTokens.Setup(r => r.GetByHashAsync(Hash, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(token);

    [Fact]
    public async Task Handle_OwnActiveToken_ShouldRevokeAndSave()
    {
        var token = DomainRefreshToken.Create(_callerId, Hash, DateTime.UtcNow.AddDays(7));
        StoredIs(token);

        await _handler.Handle(new LogoutCommand(Raw), CancellationToken.None);

        token.RevokedAt.Should().NotBeNull();
        token.ReplacedByTokenId.Should().BeNull();   // خروج لا تدوير
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForeignToken_ShouldNotRevoke()
    {
        var token = DomainRefreshToken.Create(Guid.NewGuid(), Hash, DateTime.UtcNow.AddDays(7));
        StoredIs(token);

        await _handler.Handle(new LogoutCommand(Raw), CancellationToken.None);

        token.RevokedAt.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyRevokedToken_ShouldNotRunReuseDetection()
    {
        var token = DomainRefreshToken.Create(_callerId, Hash, DateTime.UtcNow.AddDays(7));
        token.Revoke(DateTime.UtcNow);
        StoredIs(token);

        // لا يرمي — الفحص يمنع InvalidOperationException من Revoke
        await _handler.Handle(new LogoutCommand(Raw), CancellationToken.None);

        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}