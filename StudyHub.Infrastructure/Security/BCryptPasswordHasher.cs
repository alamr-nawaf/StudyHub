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
        // الهاش يأتي من القاعدة لا من المستخدم: أي شكل غير صالح فيه اعتماد مرفوض لا عطل خادم
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            return BC.EnhancedVerify(password, hash);
        }
        catch (Exception ex) when (ex is SaltParseException or ArgumentException)
        {
            // ArgumentOutOfRangeException يرث ArgumentException:
            // هاش مقتطع يجتاز فحص النسخة ثم يكسر Substring داخل المكتبة
            return false;
        }
    }
}
