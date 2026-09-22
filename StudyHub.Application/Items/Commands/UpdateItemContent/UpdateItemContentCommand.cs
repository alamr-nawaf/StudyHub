using MediatR;

namespace StudyHub.Application.Items.Commands.UpdateItemContent;

/// <summary>
/// Replaces the title and content of a note or task; a null content clears it. Never changes Kind (rule 3.2.8).
/// </summary>
// The id always comes from the route: the controller overwrites whatever the body carried
public record UpdateItemContentCommand(Guid Id, string Title, string? Content) : IRequest;
