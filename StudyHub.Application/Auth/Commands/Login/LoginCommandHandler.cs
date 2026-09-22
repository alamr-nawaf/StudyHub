using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Auth.Commands.Login;

/// <summary>
/// Applies the four login rules of §9.2: one message for every failure, IsActive checked
/// before any token is issued, Verify called on every path, and the password never logged.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // One instant for both tokens, so their lifetimes cannot drift apart
        var utcNow = DateTime.UtcNow;

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Always called, even when no user was found — rule 3. Skipping it would make an
        // unknown e-mail measurably faster to answer than a wrong password
        var passwordMatches = _passwordHasher.Verify(
            request.Password,
            user?.PasswordHash ?? _passwordHasher.DummyHash);

        // One condition for three reasons, so no branch can answer with a different message
        if (user is null || !passwordMatches || !user.IsActive)
            throw new InvalidCredentialsException();

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Role, utcNow);
        var refresh = _tokenService.GenerateRefreshToken(utcNow);

        _refreshTokenRepository.Add(
            RefreshToken.Create(user.Id, refresh.TokenHash, refresh.ExpiresAt));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            accessToken.Value,
            refresh.RawToken,
            "Bearer",
            (int)(accessToken.ExpiresAt - utcNow).TotalSeconds);
    }
}