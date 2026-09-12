using FluentValidation.TestHelper;
using StudyHub.Application.Tasks.Commands.CreateTask;
using Xunit;

namespace StudyHub.Application.Tests.Tasks;

// قاعدتان شرطيتان لا يحميهما شيء آخر: وراثة الكورس من الأب، وحصر التاريخ في UTC.
// حذف أيٍّ منهما لا يكسر بناءً ولا اختبار معالِج
public class CreateTaskCommandValidatorTests
{
    private readonly CreateTaskCommandValidator _validator = new();

    private static CreateTaskCommand Command(
        Guid? parentItemId = null,
        Guid? courseId = null,
        DateTime? dueDate = null)
        => new("Valid title", null, parentItemId, courseId, DueDate: dueDate);

    [Fact]
    public void Validate_DueDateNull_ShouldPass()
    {
        var result = _validator.TestValidate(Command());

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }

    [Fact]
    public void Validate_DueDateUtc_ShouldPass()
    {
        var utc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc);

        var result = _validator.TestValidate(Command(dueDate: utc));

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }

    // ما يصل من "+03:00" في الـ JSON
    [Fact]
    public void Validate_DueDateLocal_ShouldFail()
    {
        var local = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Local);

        var result = _validator.TestValidate(Command(dueDate: local));

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    // ما يصل من تاريخ بلا إزاحة إطلاقًا — أخطرها، لأنه ليس لحظة زمنية أصلًا
    [Fact]
    public void Validate_DueDateUnspecified_ShouldFail()
    {
        var unspecified = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Unspecified);

        var result = _validator.TestValidate(Command(dueDate: unspecified));

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    [Fact]
    public void Validate_CourseIdWithParent_ShouldFail()
    {
        var command = Command(parentItemId: Guid.NewGuid(), courseId: Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CourseId);
    }

    [Fact]
    public void Validate_ParentWithoutCourseId_ShouldPass()
    {
        var command = Command(parentItemId: Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.CourseId);
    }
}