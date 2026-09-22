using FluentValidation;

namespace StudyHub.Application.Common.Validation;

/// <summary>
/// The UTC-only rule for optional dates, shared by every validator that carries a DateTime (Requirements §14.1).
/// </summary>
public static class UtcDateRuleExtensions
{
    // Npgsql writes nothing but UTC, so refusing here is a 400 that explains itself instead
    // of a 500 at save time
    public static IRuleBuilderOptions<T, DateTime?> UtcOrNull<T>(this IRuleBuilder<T, DateTime?> rule) =>
        rule
            .Must(d => d is null || d.Value.Kind == DateTimeKind.Utc)
            .WithMessage("DueDate must be UTC: an ISO 8601 value ending in 'Z'.");
}
