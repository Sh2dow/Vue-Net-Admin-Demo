using backend.Tasks.Requests.Tasks;
using FluentValidation;

namespace backend.Tasks.Validation.Tasks;

public sealed class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Title)
            .Must(x => x == null || !string.IsNullOrWhiteSpace(x))
            .WithMessage("Title cannot be empty.");
    }
}
