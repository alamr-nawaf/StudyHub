using MediatR;
using StudyHub.Application.Auth.Commands.Login;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Auth.Commands.Refresh;

/// <summary>
/// Rotates the token pair in the order §9.3 lays down: look up by hash, then reuse
/// detection, then expiry, then the user's state, then the rotation itself in one save.
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);

        // 1. Unknown
        if (stored is null)
            throw new InvalidCredentialsException();

        // 2. Revoked by rotation means it has a successor, so whoever presents it now holds
        //    an old copy of a live chain: a theft. The mass revocation executes immediately,
        //    so it is saved before the throw. It deliberately comes before the IsActive
        //    check: both cases are "not active" and they deserve different answers
        if (stored.ReplacedByTokenId is not null)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, utcNow, cancellationToken);
            throw new InvalidCredentialsException();
        }

        // 2b. Revoked without a successor means a logout or an earlier mass revocation. The
        //     chain is already dead, so there is nothing left to steal from it. If this fired
        //     a mass revocation, anyone holding an old logged-out token could sign the user
        //     out of every device at will, and a refresh still in flight when the user tapped
        //     logout would sign them out of their other devices
        if (stored.RevokedAt is not null)
            throw new InvalidCredentialsException();

        // 3. Expired: a plain 401 — running out of time is not a theft
        if (!stored.IsActive(utcNow))
            throw new InvalidCredentialsException();

        // 4. The identity comes from the stored row, never from the token
        var user = await _userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new InvalidCredentialsException();

        // 5. The rotation
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Role, utcNow);
        var refresh = _tokenService.GenerateRefreshToken(utcNow);

        var newToken = RefreshToken.Create(user.Id, refresh.TokenHash, refresh.ExpiresAt);
        _refreshTokenRepository.Add(newToken);

        // Links the old token to the new one: the rotation chain reuse detection reads
        stored.Revoke(utcNow, newToken.Id);

        // One save is one transaction. Do not add an explicit one (§9.3)
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            accessToken.Value,
            refresh.RawToken,
            "Bearer",
            (int)(accessToken.ExpiresAt - utcNow).TotalSeconds);
    }
}