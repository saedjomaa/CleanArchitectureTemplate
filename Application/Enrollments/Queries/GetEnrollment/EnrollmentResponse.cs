namespace Application.Enrollments.Queries.GetEnrollment;

public sealed record EnrollmentResponse(Guid StudentId, Guid CourseId, DateTimeOffset EnrolledAt);
