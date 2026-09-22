using FluentAssertions;
using Moq;
using StudyHub.Application.Auth.Commands.PurgeExpiredRefreshTokens;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;

namespace StudyHub.Application.Tests.Auth.Commands.PurgeExpiredRefreshTokens;

public class PurgeExpiredRefreshTokensCommandHandlerTests
{
    private const int RetentionDays = 7;

    private readonly Mock<IRefreshTokenRepository> _refreshTokensMock = new();
    private readonly PurgeExpiredRefreshTokensCommandHandler _handler;

    public PurgeExpiredRefreshTokensCommandHandlerTests()
    {
        _handler = new PurgeExpiredRefreshTokensCommandHandler(
            _refreshTokensMock.Object,
            new RefreshTokenCleanupSettings(Enabled: true, IntervalHours: 24, RetentionDaysAfterExpiry: RetentionDays));
    }

    [Fact]
    public async Task Handle_Always_ShouldDeleteTokensExpiredBeforeRetentionDaysAgo()
    {
        // Arrange
        DateTime? cutoff = null;

        _refreshTokensMock
            .Setup(r => r.DeleteExpiredBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((c, _) => cutoff = c)
            .ReturnsAsync(0);

        var before = DateTime.UtcNow;

        // Act
        await _handler.Handle(new PurgeExpiredRefreshTokensCommand(), CancellationToken.None);

        var after = DateTime.UtcNow;

        // Assert
        cutoff.Should().NotBeNull();
        cutoff!.Value.Kind.Should().Be(DateTimeKind.Utc);

        // The literal 7 is the rule, written out rather than read back from the settings
        cutoff.Value.Should().BeOnOrAfter(before.AddDays(-7)).And.BeOnOrBefore(after.AddDays(-7));
    }

    [Fact]
    public async Task Handle_Always_ShouldReturnTheNumberOfDeletedRows()
    {
        // Arrange
        _refreshTokensMock
            .Setup(r => r.DeleteExpiredBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        // Act
        var deleted = await _handler.Handle(new PurgeExpiredRefreshTokensCommand(), CancellationToken.None);

        // Assert
        deleted.Should().Be(4);
    }
}
