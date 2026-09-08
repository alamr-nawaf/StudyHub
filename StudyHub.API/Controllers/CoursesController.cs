using MediatR;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Courses.Commands.CreateCourse;
using StudyHub.Application.Courses.Commands.DeleteCourse;

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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteCourseCommand(id), cancellationToken);
        return NoContent();
    }
}