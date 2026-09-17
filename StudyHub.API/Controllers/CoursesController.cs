using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Common.Pagination;
using StudyHub.Application.Courses.Commands.CreateCourse;
using StudyHub.Application.Courses.Commands.DeleteCourse;
using StudyHub.Application.Courses.Queries.GetCourses;
using StudyHub.Application.Courses.Queries.GetCourseTree;

namespace StudyHub.API.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoursesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCourseCommand command,
        CancellationToken cancellationToken)
    {
        var courseId = await _mediator.Send(command, cancellationToken);
        return Created($"/api/courses/{courseId}", new { courseId });
    }

    [HttpGet]
    public async Task<IActionResult> GetPage(
        int page = 1,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCoursesQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }

    // قائمة مسطّحة غير مُرقَّمة، والعميل يبني الشجرة من ParentItemId (ADR-23)
    [HttpGet("{id:guid}/tree")]
    public async Task<IActionResult> GetTree(Guid id, CancellationToken cancellationToken)
    {
        var tree = await _mediator.Send(new GetCourseTreeQuery(id), cancellationToken);
        return Ok(tree);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteCourseCommand(id), cancellationToken);
        return NoContent();
    }
}