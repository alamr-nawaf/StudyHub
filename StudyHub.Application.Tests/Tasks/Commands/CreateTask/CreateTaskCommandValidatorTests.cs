using FluentValidation.TestHelper;
using StudyHub.Application.Tasks.Commands.CreateTask;
using Xunit;

namespace StudyHub.Application.Tests.Tasks;

// Two conditional rules that nothing else guards: inheriting the course from the parent, and
// restricting the date to UTC. Deleting either breaks no build and no handler test
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

    // What arrives from a "+03:00" value in the JSON
    [Fact]
    public void Validate_DueDateLocal_ShouldFail()
    {
        var local = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Local);

        var result = _validator.TestValidate(Command(dueDate: local));

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    // What arrives from a date with no offset at all — the worst of the three, because it is
    // not an instant in time in the first place
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