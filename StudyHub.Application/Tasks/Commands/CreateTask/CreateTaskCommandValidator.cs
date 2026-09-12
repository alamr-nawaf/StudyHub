using FluentValidation;

namespace StudyHub.Application.Tasks.Commands.CreateTask;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);

        // الابن يرث كورس أبيه، فإرسال الاثنين معًا التباس لا تفضيل
        RuleFor(x => x.CourseId)
            .Null()
            .When(x => x.ParentItemId.HasValue)
            .WithMessage("A nested item inherits its parent's course.");

        // التعداد رقم في الـ JSON، فقيمة 99 تمرّ بلا هذا السطر
        RuleFor(x => x.Priority).IsInEnum();

        // Npgsql لا يكتب إلا UTC؛ الرفض هنا 400 يشرح نفسه بدل 500 عند الحفظ
        RuleFor(x => x.DueDate)
            .Must(d => d is null || d.Value.Kind == DateTimeKind.Utc)
            .WithMessage("DueDate must be UTC: an ISO 8601 value ending in 'Z'.");
    }
}