using Application.Common.Abstractions;
using Application.Common.Pagination;
using Application.Common.Results;
using Application.Courses;
using Application.Courses.Commands.CreateCourse;
using Application.Courses.Queries.GetCourse;
using Application.Courses.Queries.ListCourses;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Diagnostics;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/courses")]
public sealed class CoursesController : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Create a course")]
    public async Task<ActionResult<Guid>> Create(
        CreateCourseCommand command,
        [FromServices] ICommandHandler<CreateCourseCommand, Result<Guid>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value)
            : result.ToProblemDetails(HttpContext);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a course by ID")]
    public async Task<ActionResult<CourseResponse>> Get(
        Guid id,
        [FromServices] IQueryHandler<GetCourseQuery, Result<CourseResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetCourseQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails(HttpContext);
    }

    [HttpGet]
    [EndpointSummary("List courses")]
    public async Task<ActionResult<PagedResult<CourseResponse>>> List(
        [FromServices] IQueryHandler<ListCoursesQuery, Result<PagedResult<CourseResponse>>> handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize)
    {
        var result = await handler.HandleAsync(new ListCoursesQuery(page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails(HttpContext);
    }
}
