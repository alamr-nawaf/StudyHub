using MediatR;

namespace StudyHub.Application.Items.Commands.DeleteItem;

/// <summary>
/// Soft-deletes a note or a task and its whole subtree.
/// </summary>
public record DeleteItemCommand(Guid Id) : IRequest;