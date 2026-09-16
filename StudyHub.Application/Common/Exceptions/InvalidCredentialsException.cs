namespace StudyHub.Application.Common.Exceptions;

// يرميه معالِجا الدخول والتجديد وحدهما، وبنص واحد لكل الأسباب: إيميل مجهول،
// كلمة مرور خاطئة، حساب معطَّل، توكن منتهٍ (§9.2 و§9.3). يُترجَم إلى 401
public class InvalidCredentialsException : Exception
{
    private const string SingleMessage = "Invalid credentials.";

    // بلا باني يقبل رسالة: أي نص مختلف يعيد فتح ثغرة تعداد الحسابات
    public InvalidCredentialsException() : base(SingleMessage) { }
}