using MediatR;

namespace StudyHub.Application.Items.Queries.GetItem;

/// <summary>
/// Requests one note or task by id.
/// </summary>
public record GetItemQuery(Guid Id) : IRequest<ItemDto>;
