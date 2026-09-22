using MediatR;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;

namespace StudyHub.Application.Auth.Commands.PurgeExpiredRefreshTokens;

/// <summary>
/// The retention rule of ADR-46: a row goes once it has been expired for longer than
/// the retention window, and never because it was revoked. A revoked row is exactly what
/// reuse detection looks for, so deleting it early would turn a stolen token into an
/// unknown one — a plain 401 instead of revoking the whole chain (§9.3).
/// </summary>
public class PurgeExpiredRefreshTokensCommandHandler : IRequestHandler<PurgeExpiredRefreshTokensCommand, int>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly RefreshTokenCleanupSettings _settings;

    public PurgeExpiredRefreshTokensCommandHandler(
        IRefreshTokenRepository refreshTokens, RefreshTokenCleanupSettings settings)
    {
        _refreshTokens = refreshTokens;
        _settings = settings;
    }

    public Task<int> Handle(PurgeExpiredRefreshTokensCommand request, CancellationToken cancellationToken)
    {
        var cutoffUtc = DateTime.UtcNow.AddDays(-_settings.RetentionDaysAfterExpiry);

        return _refreshTokens.DeleteExpiredBeforeAsync(cutoffUtc, cancellationToken);
    }
}
