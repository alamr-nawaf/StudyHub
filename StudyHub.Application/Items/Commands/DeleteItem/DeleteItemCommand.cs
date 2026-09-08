using MediatR;

namespace StudyHub.Application.Items.Commands.DeleteItem;

public record DeleteItemCommand(Guid Id) : IRequest;