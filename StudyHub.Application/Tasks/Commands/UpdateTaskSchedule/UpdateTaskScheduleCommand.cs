using MediatR;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tasks.Commands.UpdateTaskSchedule;

/// <summary>
/// Replaces a task's priority and due date; a null due date clears it.
/// </summary>
// Id يأتي من المسار دائمًا؛ المتحكّم يكتب فوق أي قيمة وصلت في الجسم
public record UpdateTaskScheduleCommand(Guid Id, TaskPriority Priority, DateTime? DueDate) : IRequest;
