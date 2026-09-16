using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Auth.Commands.Login;

// يطبّق قواعد الدخول الأربع في §9.2: نص واحد لكل فشل، وفحص IsActive قبل أي توكن،
// وVerify في كل مسار، ولا تسجيل لكلمة المرور
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
        // لحظة واحدة للتوكنين معًا فلا ينحرفان
        var utcNow = DateTime.UtcNow;

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // تُستدعى دائمًا، حتى بلا مستخدم — القاعدة 3
        var passwordMatches = _passwordHasher.Verify(
            request.Password,
            user?.PasswordHash ?? _passwordHasher.DummyHash);

        // شرط واحد لثلاثة أسباب: لا فرع يستطيع أن يرمي نصًا مختلفًا
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