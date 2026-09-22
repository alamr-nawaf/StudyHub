using FluentValidation;
using StudyHub.Application.Common.Pagination;

namespace StudyHub.Application.Common.Validation;

/// <summary>
/// The page and page-size rules shared by every paginated query validator.
/// </summary>
public static class PagingRuleExtensions
{
    // A value out of range is refused with a 400 rather than silently clamped: the client
    // has to know that its request was not carried out as written
    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"PageSize must be between 1 and {Paging.MaxPageSize}.");
}
