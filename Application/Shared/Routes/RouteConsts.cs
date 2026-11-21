namespace Application.Shared.Routes;

public static class RouteConsts
{
    public const string Students = "api/students";
    public const string Courses = "api/courses";
    public const string ById = "{id:guid}";
    public const string StudentCourses = ById + "/courses";
}
