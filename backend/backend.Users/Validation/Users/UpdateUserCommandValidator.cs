using backend.Users.Requests.Users;
using FluentValidation;

namespace backend.Users.Validation.Users;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
    }
}
