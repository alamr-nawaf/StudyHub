using FluentAssertions;
using StudyHub.Domain.Authorization;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;
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
    public void HasQuotaFor_EstimateReachingExactlyTheQuota_ShouldReturnTrue()
    {
        var user = CreateSut(quota: 100);

        var hasQuota = user.HasQuotaFor(tokensUsedThisMonth: 30, estimatedTokens: 70);

        hasQuota.Should().BeTrue();
    }

    [Fact]
    public void HasQuotaFor_EstimateOneTokenOverTheQuota_ShouldReturnFalse()
    {
        var user = CreateSut(quota: 100);

        var hasQuota = user.HasQuotaFor(tokensUsedThisMonth: 30, estimatedTokens: 71);

        hasQuota.Should().BeFalse();
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