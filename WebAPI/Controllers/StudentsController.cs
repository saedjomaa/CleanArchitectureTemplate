using Application.Common.Abstractions;
using Application.Common.Pagination;
using Application.Common.Results;
using Application.Enrollments.Commands.EnrollStudent;
using Application.Enrollments.Queries.ListStudentCourses;
using Application.Students;
using Application.Students.Commands.CreateStudent;
using Application.Students.Queries.GetStudent;
using Application.Students.Queries.ListStudents;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Diagnostics;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/students")]
public sealed class StudentsController : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Create a student")]
    public async Task<ActionResult<Guid>> Create(
        CreateStudentCommand command,
        [FromServices] ICommandHandler<CreateStudentCommand, Result<Guid>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value)
            : result.ToProblemDetails(HttpContext);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a student by ID")]
    public async Task<ActionResult<StudentResponse>> Get(
        Guid id,
        [FromServices] IQueryHandler<GetStudentQuery, Result<StudentResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetStudentQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails(HttpContext);
    }

    [HttpGet]
    [EndpointSummary("List students")]
    public async Task<ActionResult<PagedResult<StudentResponse>>> List(
        [FromServices] IQueryHandler<ListStudentsQuery, Result<PagedResult<StudentResponse>>> handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize)
    {
        var result = await handler.HandleAsync(new ListStudentsQuery(page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails(HttpContext);
    }

    [HttpPost("{studentId:guid}/enrollments")]
    [EndpointSummary("Create a course enrollment for a student")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEnrollment(
        Guid studentId,
        EnrollStudentRequest request,
        [FromServices] ICommandHandler<EnrollStudentCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new EnrollStudentCommand(studentId, request.CourseId), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblemDetails(HttpContext);
    }

    [HttpGet("{id:guid}/courses")]
    [EndpointSummary("List courses a student is enrolled in")]
    public async Task<ActionResult<PagedResult<StudentCourseResponse>>> ListEnrolledCourses(
        Guid id,
        [FromServices] IQueryHandler<ListStudentCoursesQuery, Result<PagedResult<StudentCourseResponse>>> handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize)
    {
        var result = await handler.HandleAsync(new ListStudentCoursesQuery(id, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails(HttpContext);
    }
}

public sealed record EnrollStudentRequest(Guid CourseId);
