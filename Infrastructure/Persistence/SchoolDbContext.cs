using Application.Common.Abstractions;
using Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class SchoolDbContext(DbContextOptions<SchoolDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    private const int SqliteConstraintPrimaryKey = 1555;

    public IQueryable<Student> Students => Set<Student>();
    public IQueryable<Course> Courses => Set<Course>();
    public IQueryable<Enrollment> Enrollments => Set<Enrollment>();

    public void AddStudent(Student student) => Set<Student>().Add(student);

    public void AddCourse(Course course) => Set<Course>().Add(course);

    public async Task<bool> TrySaveEnrollmentAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        var entry = Set<Enrollment>().Add(enrollment);
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintPrimaryKey } &&
            exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, enrollment))
        {
            entry.State = EntityState.Detached;
            return false;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var students = modelBuilder.Entity<Student>();
        students.ToTable("Students");
        students.HasKey(student => student.Id);
        students.Property(student => student.FullName).HasMaxLength(Student.MaximumFullNameLength).IsRequired();

        var courses = modelBuilder.Entity<Course>();
        courses.ToTable("Courses");
        courses.HasKey(course => course.Id);
        courses.Property(course => course.Title).HasMaxLength(Course.MaximumTitleLength).IsRequired();

        var enrollments = modelBuilder.Entity<Enrollment>();
        enrollments.ToTable("Enrollments");
        enrollments.HasKey(enrollment => new { enrollment.StudentId, enrollment.CourseId });
        enrollments.HasOne<Student>().WithMany().HasForeignKey(enrollment => enrollment.StudentId);
        enrollments.HasOne<Course>().WithMany().HasForeignKey(enrollment => enrollment.CourseId);

        SchoolSeed.Configure(modelBuilder);
    }
}
