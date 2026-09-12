using FluentAssertions;
using StudyHub.Domain.ValueObjects;
using Xunit;

namespace StudyHub.Domain.Tests;

// الفارق بين Create و FromPersisted متعمَّد لا سهو:
// الأولى بوابة دخول، والثانية إعادة بناء من مصدر موثوق
public class EmailTests
{
    [Fact]
    public void FromPersisted_WithInvalidFormat_ShouldNotThrow()
    {
        var email = Email.FromPersisted("corrupt-row-value");

        email.Value.Should().Be("corrupt-row-value");
    }
}