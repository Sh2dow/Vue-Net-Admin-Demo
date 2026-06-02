using backend.Tasks.Requests.Tasks;
using FluentValidation;

namespace backend.Tasks.Validation.Tasks;

public sealed class AddTaskCommentCommandValidator : AbstractValidator<AddTaskCommentCommand>
{
    public AddTaskCommentCommandValidator()
    {
        RuleFor(x => x.Content).NotEmpty();
    }
}
