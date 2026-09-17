using FluentValidation;
using StudyHub.Application.Common.Pagination;

namespace StudyHub.Application.Common.Validation;

/// <summary>
/// The page and page-size rules shared by every paginated query validator.
/// </summary>
public static class PagingRuleExtensions
{
    // قيمة خارج المدى تُرفض بـ 400 ولا تُقصّ بصمت: العميل يجب أن يعرف أن طلبه لم يُنفَّذ كما كتبه
    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"PageSize must be between 1 and {Paging.MaxPageSize}.");
}
