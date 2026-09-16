using MediatR;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Auth.Commands.Logout;

// 204 في كل الحالات (§9.3، وRFC 7009). لا كشف إعادة استخدام هنا أبدًا:
// توكن ملغى يصل إلى الخروج ضغطة مكرّرة لا سرقة
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

        // مجهول، أو لغير المتصل، أو غير نشط — كلها خروج صامت.
        // فحص IsActive إلزامي هنا: Revoke على توكن ملغى ترمي InvalidOperationException → 500
        if (stored is null || stored.UserId != _currentUser.UserId || !stored.IsActive(utcNow))
            return;

        stored.Revoke(utcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}