using FluentAssertions;
using StudyHub.Domain.Authorization;
using StudyHub.Domain.Enums;

namespace StudyHub.Domain.Tests;

// The map returns an empty set for a role it does not know: correct at run time, dangerous at
// writing time, because a third role added without an entry would silently mean "no
// permissions". This test turns that silence into noise
public class RolePermissionsTests
{
    [Fact]
    public void For_EveryDeclaredRole_ShouldHaveADecision()
    {
        var decided = new Dictionary<UserRole, bool>
        {
            [UserRole.User] = false,   
            [UserRole.Admin] = true
        };

        foreach (var role in Enum.GetValues<UserRole>())
        {
            decided.Should().ContainKey(role);
            RolePermissions.For(role).Any().Should().Be(decided[role]);
        }
    }
}