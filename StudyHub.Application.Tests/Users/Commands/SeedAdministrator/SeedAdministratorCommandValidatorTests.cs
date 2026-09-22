using FluentValidation.TestHelper;
using StudyHub.Application.Users.Commands.SeedAdministrator;

namespace StudyHub.Application.Tests.Users.Commands.SeedAdministrator;

// Both rules are conditional on the value being present: absence is allowed (promoting an
// existing account), and what is present obeys the policy
public class SeedAdministratorCommandValidatorTests
{
    private readonly SeedAdministratorCommandValidator _validator = new();

    [Fact]
    public void Validate_EmailOnly_ShouldPass()
    {
        var command = new SeedAdministratorCommand("admin@test.com", null, null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WeakPassword_ShouldFail()
    {
        var command = new SeedAdministratorCommand("admin@test.com", "Admin", "weak");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_BlankFullName_ShouldFail()
    {
        var command = new SeedAdministratorCommand("admin@test.com", "  ", "Password123");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Validate_MalformedEmail_ShouldFail()
    {
        var command = new SeedAdministratorCommand("not-an-email", "Admin", "Password123");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
