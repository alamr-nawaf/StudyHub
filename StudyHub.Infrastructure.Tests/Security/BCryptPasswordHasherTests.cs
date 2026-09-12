using FluentAssertions;
using StudyHub.Infrastructure.Security;

namespace StudyHub.Infrastructure.Tests.Security;

// يثبت الحالتين اللتين سبّبتا D4 و D5: هاش مخزَّن تالف يجب أن يُرفَض لا أن يُسقط الطلب،
// وحدّ الـ 72 بايت في BCrypt يجب أن يكون مُلغى بفضل النسخة Enhanced
public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Verify_CorrectPassword_ShouldReturnTrue()
    {
        var hash = _hasher.Hash("Password123");

        var result = _hasher.Verify("Password123", hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ShouldReturnFalse()
    {
        var hash = _hasher.Hash("Password123");

        var result = _hasher.Verify("Password124", hash);

        result.Should().BeFalse();
    }

    // D4: الهاش التالف في القاعدة له جوابان صحيحان فقط — صح أو خطأ، لا استثناء
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("$2a$12$short")]
    [InlineData("$1$abc$def")]
    public void Verify_MalformedStoredHash_ShouldReturnFalse(string malformedHash)
    {
        var act = () => _hasher.Verify("Password123", malformedHash);

        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    // D5: كلمتان تشتركان في أول 72 بايت — القياسية تخلط بينهما، والمحسّنة لا
    [Fact]
    public void Verify_PasswordSharingFirst72Bytes_ShouldReturnFalse()
    {
        var prefix = new string('a', 72);
        var hash = _hasher.Hash(prefix + "X");

        var result = _hasher.Verify(prefix + "Y", hash);

        result.Should().BeFalse();
    }
}