using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Items.Commands.DeleteItem;
using StudyHub.Application.Items.Commands.UpdateItemContent;
using StudyHub.Application.Items.Queries.GetItem;
using StudyHub.Application.Items.Queries.GetItemTree;
using StudyHub.Application.Items.Queries.GetRootItems;

namespace StudyHub.API.Controllers;

/// <summary>
/// One controller for everything that does not care whether an item is a note or a task:
/// reading, editing the content, and deleting.
/// </summary>
[ApiController]
[Route("api/items")]
public class ItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemsController(IMediator mediator) => _mediator = mediator;

    // Standalone items only; what sits under a course is read from that course's tree
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

    // The id comes from the route, not from the body: `with` overwrites whatever id the
    // client sent
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateContent(
        Guid id,
        UpdateItemContentCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteItemCommand(id), cancellationToken);
        return NoContent();
    }
}
