using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.API.Common;

// نسخة مؤقتة للتطوير فقط. تُستبدل بالكامل عند بناء الـ JWT،
// ولن يتغيّر معها سطر واحد في طبقة التطبيق.
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid UserId
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?
                .Request.Headers["X-User-Id"].FirstOrDefault();

            if (!Guid.TryParse(raw, out var userId))
                throw new ForbiddenException("Missing or invalid X-User-Id header.");

            return userId;
        }
    }
}