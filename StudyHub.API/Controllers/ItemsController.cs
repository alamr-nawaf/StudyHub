using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Items.Commands.DeleteItem;
using StudyHub.Application.Items.Queries.GetItem;
using StudyHub.Application.Items.Queries.GetItemTree;
using StudyHub.Application.Items.Queries.GetRootItems;

namespace StudyHub.API.Controllers;

// متحكّم واحد للقراءة والحذف: العمليتان لا تفرّقان بين ملاحظة ومهمة
[ApiController]
[Route("api/items")]
public class ItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemsController(IMediator mediator) => _mediator = mediator;

    // العناصر المستقلة فقط؛ ما تحت كورس يُقرأ من شجرة الكورس
    [HttpGet]
    public async Task<IActionResult> GetRootPage(
        int page = 1,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetRootItemsQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _mediator.Send(new GetItemQuery(id), cancellationToken);
        return Ok(item);
    }

    [HttpGet("{id:guid}/tree")]
    public async Task<IActionResult> GetTree(Guid id, CancellationToken cancellationToken)
    {
        var tree = await _mediator.Send(new GetItemTreeQuery(id), cancellationToken);
        return Ok(tree);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteItemCommand(id), cancellationToken);
        return NoContent();
    }
}
