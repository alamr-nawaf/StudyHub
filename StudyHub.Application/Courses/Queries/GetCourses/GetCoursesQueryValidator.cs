using FluentValidation;
using StudyHub.Application.Common.Validation;

namespace StudyHub.Application.Courses.Queries.GetCourses;

/// <summary>
/// Validates the paging values of <see cref="GetCoursesQuery"/>.
/// </summary>
public class GetCoursesQueryValidator : AbstractValidator<GetCoursesQuery>
{
    public GetCoursesQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
