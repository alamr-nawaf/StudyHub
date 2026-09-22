using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.API.Common;

/// <summary>
/// The identity comes from the "sub" claim of a token the middleware has already validated.
/// Application is unchanged by this: it still asks ICurrentUserService and knows nothing of
/// HTTP.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid UserId
    {
        get
        {
            // "sub" literally: inbound claim mapping is off, so there is no alternative name
            var raw = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

            // Only reachable if a token without sub got through: the middleware refuses
            // everything else with a 401 long before this point
            if (!Guid.TryParse(raw, out var userId))
                throw new ForbiddenException("The token does not carry a usable user id.");

            return userId;
        }
    }
}