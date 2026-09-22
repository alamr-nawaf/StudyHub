using FluentValidation.TestHelper;
using StudyHub.Application.Tasks.Commands.UpdateTaskStatus;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Tasks.Commands.UpdateTaskStatus;

// An absent field arrives as null, and without NotNull it silently became Pending: a rule
// nothing else guards (A24)
public class UpdateTaskStatusCommandValidatorTests
{
    private readonly UpdateTaskStatusCommandValidator _validator = new();

    private static UpdateTaskStatusCommand Command(StudyTaskStatus? status) =>
        new(Guid.NewGuid(), status);

    [Fact]
    public void Validate_StatusMissing_ShouldFail()
    {
        var result = _validator.TestValidate(Command(null));

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Validate_StatusPending_ShouldPass()
    {
        var result = _validator.TestValidate(Command(StudyTaskStatus.Pending));

        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }
}
