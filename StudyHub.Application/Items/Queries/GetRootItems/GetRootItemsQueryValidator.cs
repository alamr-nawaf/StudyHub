using FluentValidation;
using StudyHub.Application.Common.Validation;

namespace StudyHub.Application.Items.Queries.GetRootItems;

/// <summary>
/// Validates the paging values of <see cref="GetRootItemsQuery"/>.
/// </summary>
public class GetRootItemsQueryValidator : AbstractValidator<GetRootItemsQuery>
{
    public GetRootItemsQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
