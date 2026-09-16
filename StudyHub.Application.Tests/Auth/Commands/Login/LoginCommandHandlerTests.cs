using FluentAssertions;
using Moq;
using StudyHub.Application.Auth.Commands.Login;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Auth.Commands.Login;

// يثبت قواعد §9.2 الأربع، وأن الفشل لا يحفظ شيئًا
public class LoginCommandHandlerTests
{
    private const string DummyHash = "$2a$12$dummy";
    private const string StoredHash = "$2a$12$stored";
    private const string Email = "nawaf@test.com";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _hasher.Setup(h => h.DummyHash).Returns(DummyHash);

        _tokens.Setup(t => t.GenerateAccessToken(
                   It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<DateTime>()))
               .Returns((Guid _, UserRole _, DateTime now) =>
                   new AccessToken("access", now.AddMinutes(15)));

        _tokens.Setup(t => t.GenerateRefreshToken(It.IsAny<DateTime>()))
               .Returns((DateTime now) =>
                   new RefreshTokenResult("raw", "hash", now.AddDays(7)));

        _handler = new LoginCommandHandler(
            _users.Object, _hasher.Object, _tokens.Object,
            _refreshTokens.Object, _unitOfWork.Object);
    }

    private static User ActiveUser() => User.Create("Nawaf", Email, StoredHash, 100_000);

    [Fact]
    public async Task Handle_ValidCredentials_ShouldReturnPairAndSaveRefreshToken()
    {
        var user = ActiveUser();
        _users.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Correct123", StoredHash)).Returns(true);

        var result = await _handler.Handle(new LoginCommand(Email, "Correct123"), CancellationToken.None);

        result.AccessToken.Should().Be("access");
        result.RefreshToken.Should().Be("raw");
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(900);

        // المحفوظ هاش لا خام
        _refreshTokens.Verify(r => r.Add(It.Is<RefreshToken>(
            t => t.TokenHash == "hash" && t.UserId == user.Id)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WrongPassword_ShouldThrowInvalidCredentials()
    {
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(ActiveUser());
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), StoredHash)).Returns(false);

        var act = () => _handler.Handle(new LoginCommand(Email, "Wrong123"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _refreshTokens.Verify(r => r.Add(It.IsAny<RefreshToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownEmail_ShouldThrowInvalidCredentialsAndStillVerify()
    {
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), DummyHash)).Returns(false);

        var act = () => _handler.Handle(new LoginCommand("ghost@test.com", "Whatever123"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();

        // إثبات القاعدة 3: Verify نُفِّذت على الوهمي رغم غياب المستخدم
        _hasher.Verify(h => h.Verify("Whatever123", DummyHash), Times.Once);
    }

    [Fact]
    public async Task Handle_InactiveUser_ShouldThrowInvalidCredentials()
    {
        var user = ActiveUser();
        user.Deactivate();
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), StoredHash)).Returns(true);

        var act = () => _handler.Handle(new LoginCommand(Email, "Correct123"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}