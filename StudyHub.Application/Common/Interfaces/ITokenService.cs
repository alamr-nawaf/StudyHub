using StudyHub.Domain.Enums;

namespace StudyHub.Application.Common.Interfaces;

// ITokenService: التطبيق يطلب توكنات وهاشات، والبنية التحتية تعرف كيف تُصنع.
// الدور وسيط لأن claim الدور جزء من التوكن (المتطلبات §9.1 القرار 5، وADR-21)
public interface ITokenService
{
    AccessToken GenerateAccessToken(Guid userId, UserRole role, DateTime utcNow);

    // الخام والهاش والصلاحية من نداء واحد؛ الخام وحده يُعطى للعميل ولا يُخزَّن أبدًا
    RefreshTokenResult GenerateRefreshToken(DateTime utcNow);

    // SHA-256 بصيغة hex: 64 محرفًا تطابق طول العمود في RefreshTokenConfiguration
    string HashRefreshToken(string rawToken);
}