using FluentValidation.TestHelper;
using StudyHub.Application.Notes.Commands.CreateNote;
using Xunit;

namespace StudyHub.Application.Tests.Notes;

// الملاحظة بلا DueDate، فتبقى قاعدة وراثة الكورس وحدها
public class CreateNoteCommandValidatorTests
{
    private readonly CreateNoteCommandValidator _validator = new();

    [Fact]
    public void Validate_CourseIdWithParent_ShouldFail()
    {
        var command = new CreateNoteCommand("Valid title", null, Guid.NewGuid(), Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CourseId);
    }

    [Fact]
    public void Validate_ParentWithoutCourseId_ShouldPass()
    {
        var command = new CreateNoteCommand("Valid title", null, Guid.NewGuid(), null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.CourseId);
    }
}