namespace Application.Enrollments.Queries.ListStudentCourses;

public sealed record StudentCourseResponse(Guid CourseId, string Title, DateTimeOffset EnrolledAt);
