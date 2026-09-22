using MediatR;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Auth.Commands.Logout;

/// <summary>
/// 204 in every case (§9.3, and RFC 7009). Reuse detection never runs here: a revoked token
/// arriving at logout is a repeated click, not a theft.
/// </summary>
public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ICurrentUserService currentUser,
        ITokenService tokenService,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);

        // Unknown, someone else's, or not active — all of them are a silent success.
        // The IsActive check is required: Revoke on an already-revoked token throws
        // InvalidOperationException, which would surface as a 500
        if (stored is null || stored.UserId != _currentUser.UserId || !stored.IsActive(utcNow))
            return;

        stored.Revoke(utcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}