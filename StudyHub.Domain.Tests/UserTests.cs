using FluentAssertions;
using StudyHub.Domain.Authorization;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;
namespace StudyHub.Domain.Tests;

public class UserTests
{
    private static User CreateSut(int quota = 100)
        => User.Create("Ahmed Ali", "Ahmed@Test.COM", "hash", quota);

    // The counter is written only by the atomic UPDATE in Infrastructure (ADR-37),
    // so a user who has already spent is built the way EF Core builds one when it
    // reads the row back: straight into the private setter.
    private static User WithTokensUsed(int used, int quota = 100)
    {
        var user = CreateSut(quota);
        typeof(User).GetProperty(nameof(User.TokensUsedThisMonth))!.SetValue(user, used);
        return user;
    }

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
    public void HasQuotaFor_EstimateReachingExactlyTheQuota_ShouldReturnTrue()
    {
        var user = WithTokensUsed(used: 30, quota: 100);

        var hasQuota = user.HasQuotaFor(70);

        hasQuota.Should().BeTrue();
    }

    [Fact]
    public void HasQuotaFor_EstimateOneTokenOverTheQuota_ShouldReturnFalse()
    {
        var user = WithTokensUsed(used: 30, quota: 100);

        var hasQuota = user.HasQuotaFor(71);

        hasQuota.Should().BeFalse();
    }

    [Fact]
    public void HasQuotaFor_AskedTwice_ShouldNotChangeTheCounter()
    {
        var user = WithTokensUsed(used: 30, quota: 100);

        user.HasQuotaFor(70);
        user.HasQuotaFor(500);

        user.TokensUsedThisMonth.Should().Be(30);
    }

    [Fact]
    public void HasQuotaFor_AfterAResetIntoANewMonth_ShouldReturnTrueAgain()
    {
        var user = WithTokensUsed(used: 100, quota: 100);
        user.HasQuotaFor(1).Should().BeFalse();

        user.ResetQuotaIfNeeded(DateTime.UtcNow.AddMonths(1));

        user.HasQuotaFor(100).Should().BeTrue();
    }

    [Fact]
    public void ResetQuotaIfNeeded_InSameMonth_ShouldKeepCounter()
    {
        var user = WithTokensUsed(used: 50);

        user.ResetQuotaIfNeeded(DateTime.UtcNow);

        user.TokensUsedThisMonth.Should().Be(50);
    }

    [Fact]
    public void ResetQuotaIfNeeded_InNewMonth_ShouldResetCounter()
    {
        var user = WithTokensUsed(used: 50);

        user.ResetQuotaIfNeeded(DateTime.UtcNow.AddMonths(1));

        user.TokensUsedThisMonth.Should().Be(0);
    }
    [Fact]
    public void Create_ShouldDefaultToUserRole()
    {
        var user = CreateSut();

        user.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public void PromoteToAdmin_ShouldChangeRole()
    {
        var user = CreateSut();

        user.PromoteToAdmin();

        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void Can_RegularUser_ShouldNotHaveAdminPermission()
    {
        var user = CreateSut();

        user.Can(Permissions.UsersDeactivate).Should().BeFalse();
    }

    [Fact]
    public void Can_Admin_ShouldHaveAdminPermission()
    {
        var user = CreateSut();
        user.PromoteToAdmin();

        user.Can(Permissions.UsersDeactivate).Should().BeTrue();
    }
}