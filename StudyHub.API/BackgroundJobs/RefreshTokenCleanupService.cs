using MediatR;
using StudyHub.Application.Auth.Commands.PurgeExpiredRefreshTokens;
using StudyHub.Application.Common.Settings;

namespace StudyHub.API.BackgroundJobs;

/// <summary>
/// Runs the refresh-token cleanup once at startup and then every IntervalHours (ADR-46).
/// It is only a trigger: it decides nothing and sends one command, so the retention rule
/// stays in a handler that unit tests can reach.
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshTokenCleanupSettings _settings;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        RefreshTokenCleanupSettings settings,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Refresh-token cleanup is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_settings.IntervalHours));

        // Once at startup, then on the timer: a process that is restarted daily would
        // otherwise never reach its first tick
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // This service is a singleton and DbContext is scoped, so every run needs its
            // own scope rather than a captured service
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var deleted = await mediator.Send(new PurgeExpiredRefreshTokensCommand(), cancellationToken);

            _logger.LogInformation("Refresh-token cleanup removed {Count} row(s).", deleted);
        }
        catch (Exception exception)
        {
            // Since .NET 8 an exception escaping a BackgroundService stops the whole host
            // by default. A cleanup that cannot reach the database must never take the API
            // down: it is logged and the next run tries again.
            _logger.LogWarning(exception, "Refresh-token cleanup failed; it will run again on the next tick.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a failure
            return false;
        }
    }
}
