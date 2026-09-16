namespace StudyHub.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
    // هاش وهمي صالح البنية وبنفس معامل العمل، للمقارنة حين لا يوجد مستخدم (§9.2 القاعدة 3)
    string DummyHash { get; }
}