using FluentAssertions;
using StudyHub.Domain.Entities;

namespace StudyHub.Domain.Tests;

public class UserTests
{
    private static User CreateSut(int quota = 100)
        => User.Create("Ahmed Ali", "Ahmed@Test.COM", "hash", quota);

    [Fact]
    public void Create_WithMixedCaseEmail_ShouldNormalizeToLowercase()
    {
        var user = CreateSut();

        user.Email.Value.Should().Be("ahmed@test.com");
    }

    [Fact]
    public void Create_WithInvalidEmail_ShouldThrowArgumentException()
    {
        var act = () => User.Create("Ahmed", "not-an-email", "hash", 100);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConsumeTokens_WithinQuota_ShouldIncreaseCounter()
    {
        var user = CreateSut(quota: 100);

        user.ConsumeTokens(30);

        user.TokensUsedThisMonth.Should().Be(30);
    }

    [Fact]
    public void ConsumeTokens_ExceedingQuota_ShouldThrowInvalidOperationException()
    {
        var user = CreateSut(quota: 100);

        var act = () => user.ConsumeTokens(101);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResetQuotaIfNeeded_InSameMonth_ShouldKeepCounter()
    {
        var user = CreateSut();
        user.ConsumeTokens(50);

        user.ResetQuotaIfNeeded(DateTime.UtcNow);

        user.TokensUsedThisMonth.Should().Be(50);
    }

    [Fact]
    public void ResetQuotaIfNeeded_InNewMonth_ShouldResetCounter()
    {
        var user = CreateSut();
        user.ConsumeTokens(50);

        user.ResetQuotaIfNeeded(DateTime.UtcNow.AddMonths(1));

        user.TokensUsedThisMonth.Should().Be(0);
    }
}