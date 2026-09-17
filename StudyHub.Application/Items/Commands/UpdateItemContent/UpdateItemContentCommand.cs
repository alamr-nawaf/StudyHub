using MediatR;

namespace StudyHub.Application.Items.Commands.UpdateItemContent;

/// <summary>
/// Replaces the title and content of a note or task; a null content clears it. Never changes Kind (rule 3.2.8).
/// </summary>
// Id يأتي من المسار دائمًا؛ المتحكّم يكتب فوق أي قيمة وصلت في الجسم
public record UpdateItemContentCommand(Guid Id, string Title, string? Content) : IRequest;
