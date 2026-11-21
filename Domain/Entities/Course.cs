namespace Domain.Entities;

public sealed class Course
{
    public const int MaximumTitleLength = 120;

    private Course() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;

    public static Course Create(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(title.Length, MaximumTitleLength);

        return new Course
        {
            Id = Guid.NewGuid(),
            Title = title.Trim()
        };
    }
}
