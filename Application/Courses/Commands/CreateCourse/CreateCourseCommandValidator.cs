using Domain.Entities;
using FluentValidation;

namespace Application.Courses.Commands.CreateCourse;

internal sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Course.MaximumTitleLength);
    }
}
