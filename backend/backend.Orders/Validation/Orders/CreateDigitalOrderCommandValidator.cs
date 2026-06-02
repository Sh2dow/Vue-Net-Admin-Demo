using System.Linq;
using backend.Domain.Models;
using backend.Orders.Requests.Orders;
using FluentValidation;

namespace backend.Orders.Validation.Orders;

public sealed class CreateDigitalOrderCommandValidator : AbstractValidator<CreateDigitalOrderCommand>
{
    public CreateDigitalOrderCommandValidator()
    {
        RuleFor(x => x.TotalAmount).GreaterThan(0);
        RuleFor(x => x.DownloadUrl).NotEmpty();
    }

    public CommandResult<object> ValidateCommand(CreateDigitalOrderCommand command)
    {
        var result = Validate(command);
        if (result.IsValid)
            return CommandResult<object>.Success(new object());

        var errors = result.Errors
            .Select(x => new ResultError("validation", x.ErrorMessage, x.PropertyName))
            .ToList();

        return CommandResult<object>.Failure(errors);
    }
}
