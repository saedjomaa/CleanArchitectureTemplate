namespace Domain.Entities;

public sealed class Enrollment
{
    private Enrollment() { }

    internal Enrollment(Guid studentId, Guid courseId, DateTimeOffset enrolledAt)
    {
        StudentId = studentId;
        CourseId = courseId;
        EnrolledAt = enrolledAt;
    }

    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public DateTimeOffset EnrolledAt { get; private set; }
}
