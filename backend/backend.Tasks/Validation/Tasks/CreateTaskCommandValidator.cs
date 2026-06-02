using backend.Tasks.Requests.Tasks;
using FluentValidation;

namespace backend.Tasks.Validation.Tasks;

public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
    }
}
