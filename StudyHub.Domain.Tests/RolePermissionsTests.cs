using FluentAssertions;
using StudyHub.Domain.Authorization;
using StudyHub.Domain.Enums;

namespace StudyHub.Domain.Tests;

// الخريطة ترجع فارغًا لدور لا تعرفه — صحيح وقت التشغيل، وخطر وقت الكتابة:
// دور ثالث يُضاف بلا مدخل يصير "بلا صلاحيات" بصمت. هذا الاختبار يجعله ضجيجًا
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