using backend.Users.Requests.Users;
using FluentValidation;

namespace backend.Users.Validation.Users;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Subject).NotEmpty();
        RuleFor(x => x.Username).NotEmpty();
    }
}
