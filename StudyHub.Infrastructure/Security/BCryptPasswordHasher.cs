using BC = BCrypt.Net.BCrypt;
using SaltParseException = BCrypt.Net.SaltParseException;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    // Enhanced تعمل تجزئة مسبقة للمدخل، فيختفي حدّ الـ 72 بايت
    public string Hash(string password) => BC.EnhancedHashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BC.EnhancedVerify(password, hash);
        }
        catch (SaltParseException)
        {
            // هاش تالف أو بصيغة غريبة — رفض الدخول، لا إسقاط الطلب
            return false;
        }
    }
}