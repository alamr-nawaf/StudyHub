using FluentValidation.TestHelper;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Courses.Queries.GetCourses;

namespace StudyHub.Application.Tests.Courses.Queries.GetCourses;

// حدود قواعد الترقيم المشتركة تُختبر هنا مرة واحدة؛ كل استعلام مُرقَّم آخر يستعمل الامتداد نفسه
public class GetCoursesQueryValidatorTests
{
    private readonly GetCoursesQueryValidator _validator = new();

    [Fact]
    public void Validate_PageSizeAtMaximum_ShouldPass()
    {
        var result = _validator.TestValidate(new GetCoursesQuery(1, Paging.MaxPageSize));

        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_PageSizeAboveMaximum_ShouldFail()
    {
        var result = _validator.TestValidate(new GetCoursesQuery(1, Paging.MaxPageSize + 1));

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_PageZero_ShouldFail()
    {
        var result = _validator.TestValidate(new GetCoursesQuery(0, Paging.DefaultPageSize));

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }
}
