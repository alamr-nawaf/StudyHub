using FluentValidation;

namespace StudyHub.Application.Common.Validation;

/// <summary>
/// The password policy in one place. Registration and administrator seeding both enforce it,
/// and two copies of a policy drift apart.
/// </summary>
public static class PasswordRuleExtensions
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
}
