using MediatR;
using StudyHub.Application.Auth.Commands.Login;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Auth.Commands.Refresh;

// يدوّر زوج التوكنات حسب §9.3: بحث بالهاش، ثم كشف إعادة استخدام، ثم انتهاء،
// ثم حالة المستخدم، ثم تدوير بحفظ واحد
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

        // 1. غير موجود
        if (stored is null)
            throw new InvalidCredentialsException();

        // 2. ملغى = السلسلة مسروقة. الإلغاء الجماعي ينفّذ فورًا، فيُحفظ قبل الرمي.
        //    يسبق فحص IsActive عمدًا: الاثنان "غير نشط" ولهما ردّان مختلفان
        if (stored.RevokedAt is not null)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, utcNow, cancellationToken);
            throw new InvalidCredentialsException();
        }

        // 3. منتهٍ: 401 وحدها — الانتهاء ليس سرقة
        if (!stored.IsActive(utcNow))
            throw new InvalidCredentialsException();

        // 4. الهوية من الصف المخزَّن لا من التوكن
        var user = await _userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new InvalidCredentialsException();

        // 5. التدوير
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Role, utcNow);
        var refresh = _tokenService.GenerateRefreshToken(utcNow);

        var newToken = RefreshToken.Create(user.Id, refresh.TokenHash, refresh.ExpiresAt);
        _refreshTokenRepository.Add(newToken);

        // يربط القديم بالجديد — سلسلة التدوير التي يقرأها كشف إعادة الاستخدام
        stored.Revoke(utcNow, newToken.Id);

        // حفظ واحد = معاملة واحدة. لا تضف معاملة صريحة (§9.3)
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            accessToken.Value,
            refresh.RawToken,
            "Bearer",
            (int)(accessToken.ExpiresAt - utcNow).TotalSeconds);
    }
}