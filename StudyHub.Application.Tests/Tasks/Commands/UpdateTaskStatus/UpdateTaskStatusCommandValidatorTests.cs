using FluentValidation.TestHelper;
using StudyHub.Application.Tasks.Commands.UpdateTaskStatus;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Tasks.Commands.UpdateTaskStatus;

// الحقل الغائب يصل كـ null، وبلا NotNull كان يصير Pending بصمت: قاعدة لا يحرسها شيء آخر
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
