using MediatR;

namespace StudyHub.Application.Items.Queries.GetItemTree;

/// <summary>
/// Requests a note or task together with all of its descendants, as a flat list (ADR-23).
/// </summary>
public record GetItemTreeQuery(Guid Id) : IRequest<IReadOnlyList<ItemDto>>;
