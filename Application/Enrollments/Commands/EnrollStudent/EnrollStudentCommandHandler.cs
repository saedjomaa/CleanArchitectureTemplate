using Application.Common.Abstractions;
using Application.Common.Results;
using Application.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Application.Enrollments.Commands.EnrollStudent;

internal sealed class EnrollStudentCommandHandler(IApplicationDbContext db, TimeProvider clock)
    : ICommandHandler<EnrollStudentCommand, Result>
{
    public async Task<Result> HandleAsync(EnrollStudentCommand command, CancellationToken cancellationToken)
    {
        var student = await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(student => student.Id == command.StudentId, cancellationToken);
        if (student is null)
        {
            return Result.Failure(StudentErrors.NotFound);
        }

        var course = await db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(course => course.Id == command.CourseId, cancellationToken);
        if (course is null)
        {
            return Result.Failure(CourseErrors.NotFound);
        }

        var enrollment = student.Enroll(course, clock.GetUtcNow());
        return await db.TrySaveEnrollmentAsync(enrollment, cancellationToken)
            ? Result.Success()
            : Result.Failure(EnrollmentErrors.AlreadyEnrolled);
    }
}
