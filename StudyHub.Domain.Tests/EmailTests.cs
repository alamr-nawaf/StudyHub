using FluentAssertions;
using StudyHub.Domain.ValueObjects;
using Xunit;

namespace StudyHub.Domain.Tests;

// The difference between Create and FromPersisted is deliberate, not an oversight: the first
// is a gate for input, the second rebuilds from a source that is already trusted
public class EmailTests
{
    [Fact]
    public void FromPersisted_WithInvalidFormat_ShouldNotThrow()
    {
        var email = Email.FromPersisted("corrupt-row-value");

        email.Value.Should().Be("corrupt-row-value");
    }
}