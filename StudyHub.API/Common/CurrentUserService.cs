using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.API.Common;

// الهوية من claim الـ sub في توكن تحقّقت منه الوسطية.
// طبقة التطبيق لا تتغيّر: ما زالت تسأل ICurrentUserService ولا تعرف HTTP
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid UserId
    {
        get
        {
            // "sub" حرفيًا: التحويل الوارد مُطفأ، فلا اسم بديل
            var raw = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

            // لا يقع إلا إذا مرّ توكن بلا sub — الوسطية ترفض الباقي بـ 401 قبل هنا
            if (!Guid.TryParse(raw, out var userId))
                throw new ForbiddenException("The token does not carry a usable user id.");

            return userId;
        }
    }
}