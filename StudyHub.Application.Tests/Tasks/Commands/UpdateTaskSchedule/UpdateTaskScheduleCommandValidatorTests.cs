using FluentValidation.TestHelper;
using StudyHub.Application.Tasks.Commands.UpdateTaskSchedule;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Tasks.Commands.UpdateTaskSchedule;

// قاعدة UTC لها فرع null، فتحتاج إثباتًا لكل فرع: حذفها لا يكسر بناءً ولا اختبار معالِج.
// وnull في Priority يعني حقلًا غائبًا لا قيمة مقصودة، بعكس null في DueDate
public class UpdateTaskScheduleCommandValidatorTests
{
    private readonly UpdateTaskScheduleCommandValidator _validator = new();

    private static UpdateTaskScheduleCommand Command(DateTime? dueDate) =>
        new(Guid.NewGuid(), TaskPriority.Medium, dueDate);

    [Fact]
    public void Validate_PriorityMissing_ShouldFail()
    {
        var command = new UpdateTaskScheduleCommand(Guid.NewGuid(), null, null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void Validate_PriorityLow_ShouldPass()
    {
        var command = new UpdateTaskScheduleCommand(Guid.NewGuid(), TaskPriority.Low, null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void Validate_DueDateUtc_ShouldPass()
    {
        var utc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc);

        var result = _validator.TestValidate(Command(utc));

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }

    // ما يصل من "+03:00" في الـ JSON
    [Fact]
    public void Validate_DueDateLocal_ShouldFail()
    {
        var local = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Local);

        var result = _validator.TestValidate(Command(local));

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    // ما يصل من تاريخ بلا إزاحة إطلاقًا
    [Fact]
    public void Validate_DueDateUnspecified_ShouldFail()
    {
        var unspecified = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Unspecified);

        var result = _validator.TestValidate(Command(unspecified));

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    // null يمسح التاريخ، وهو طلب صحيح لا خطأ
    [Fact]
    public void Validate_DueDateNull_ShouldPass()
    {
        var result = _validator.TestValidate(Command(null));

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }
}
