using FluentAssertions;
using StudyHub.Infrastructure.Security;

namespace StudyHub.Infrastructure.Tests.Security;

// Proves the two cases behind D4 and D5: a corrupt stored hash must be rejected rather than
// bring the request down, and BCrypt's 72-byte truncation must be gone thanks to the Enhanced
// variant
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

    // D4: a corrupt hash in the database has only two correct answers — true or false, never
    // an exception
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

    // D5: two passwords sharing their first 72 bytes — the standard variant confuses them, the
    // enhanced one does not
    [Fact]
    public void Verify_PasswordSharingFirst72Bytes_ShouldReturnFalse()
    {
        var prefix = new string('a', 72);
        var hash = _hasher.Hash(prefix + "X");

        var result = _hasher.Verify(prefix + "Y", hash);

        result.Should().BeFalse();
    }
}