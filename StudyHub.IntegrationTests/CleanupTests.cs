using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Auth.Commands.PurgeExpiredRefreshTokens;
using StudyHub.Domain.Entities;
using StudyHub.IntegrationTests.Infrastructure;

namespace StudyHub.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class CleanupTests
{
    private readonly IntegrationTestFixture _fixture;

    public CleanupTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Purge_MixedTokens_ShouldDeleteOnlyThoseExpiredBeyondRetention()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        var now = DateTime.UtcNow;

        // The raw token is never stored, so these stand in for hashes; nothing here has to
        // be a real token, only unique
        var run = Guid.NewGuid().ToString("N");
        var longExpired = $"it-{run}-expired-8-days-ago";
        var recentlyExpired = $"it-{run}-expired-6-days-ago";
        var active = $"it-{run}-still-active";
        var revokedButFresh = $"it-{run}-revoked-but-fresh";

        await _fixture.Factory.InScopeAsync(async context =>
        {
            // Well past the 7-day retention: the only row that may go
            context.Add(RefreshToken.Create(user.Id, longExpired, now.AddDays(-8)));

            // Expired, but inside the retention window: reuse detection may still need it
            context.Add(RefreshToken.Create(user.Id, recentlyExpired, now.AddDays(-6)));

            // Alive
            context.Add(RefreshToken.Create(user.Id, active, now.AddDays(7)));

            // Revoked but not expired: revocation is not what the rule reads (ADR-46)
            var revoked = RefreshToken.Create(user.Id, revokedButFresh, now.AddDays(7));
            revoked.Revoke(now);
            context.Add(revoked);

            await context.SaveChangesAsync();
        });

        // Act — through the mediator, exactly as the background job sends it
        int deleted;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            deleted = await mediator.Send(new PurgeExpiredRefreshTokensCommand());
        }

        // Assert
        deleted.Should().Be(1);

        await _fixture.Factory.InScopeAsync(async context =>
        {
            var remaining = await context.RefreshTokens
                .Where(t => t.UserId == user.Id)
                .Select(t => t.TokenHash)
                .ToListAsync();

            remaining.Should().NotContain(longExpired);

            // A row must outlive its revocation, not only its expiry: deleting a revoked
            // row early turns a stolen token into an unknown one (§9.3, ADR-46)
            remaining.Should().Contain(recentlyExpired);
            remaining.Should().Contain(active);
            remaining.Should().Contain(revokedButFresh);
        });
    }
}
