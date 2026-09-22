using FluentValidation.TestHelper;
using StudyHub.Application.Tasks.Commands.UpdateTaskSchedule;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Tasks.Commands.UpdateTaskSchedule;

// The UTC rule has a null branch, so each branch needs its own proof: deleting the rule breaks
// no build and no handler test. And null in Priority means an absent field rather than a
// deliberate value, the opposite of null in DueDate
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

    // What arrives from a "+03:00" value in the JSON
    [Fact]
    public void Validate_DueDateLocal_ShouldFail()
    {
        var local = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Local);

        var result = _validator.TestValidate(Command(local));

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    // What arrives from a date with no offset at all
    [Fact]
    public void Validate_DueDateUnspecified_ShouldFail()
    {
        var unspecified = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Unspecified);

        var result = _validator.TestValidate(Command(unspecified));

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    // null clears the date, which is a valid request rather than an error
    [Fact]
    public void Validate_DueDateNull_ShouldPass()
    {
        var result = _validator.TestValidate(Command(null));

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }
}
