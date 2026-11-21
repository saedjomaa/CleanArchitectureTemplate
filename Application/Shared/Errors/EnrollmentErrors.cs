using Application.Common.Results;

namespace Application.Shared.Errors;

internal static class EnrollmentErrors
{
    public static readonly Error AlreadyEnrolled = new(
        "Enrollments.AlreadyEnrolled", "The student is already enrolled in this course.", ErrorType.Conflict);
}
