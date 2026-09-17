using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Items.Commands.DeleteItem;

namespace StudyHub.API.Controllers;

// متحكّم واحد للحذف: العملية لا تفرّق بين ملاحظة ومهمة
[ApiController]
[Route("api/items")]
public class ItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemsController(IMediator mediator) => _mediator = mediator;

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteItemCommand(id), cancellationToken);
        return NoContent();
    }
}