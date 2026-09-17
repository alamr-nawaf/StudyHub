using MediatR;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskStatus;

/// <summary>
/// Sets the status of a task.
/// </summary>
// Id يأتي من المسار دائمًا؛ المتحكّم يكتب فوق أي قيمة وصلت في الجسم.
// Status قابل لـ null ليُفرَّق الحقل الغائب عن القيمة 0: بلا ذلك يصير الغياب Pending بصمت
public record UpdateTaskStatusCommand(Guid Id, StudyTaskStatus? Status) : IRequest;
