using System.Linq;
using backend.Domain.Models;
using backend.Orders.Requests.Orders;
using FluentValidation;

namespace backend.Orders.Validation.Orders;

public sealed class CreatePhysicalOrderCommandValidator : AbstractValidator<CreatePhysicalOrderCommand>
{
    public CreatePhysicalOrderCommandValidator()
    {
        RuleFor(x => x.TotalAmount).GreaterThan(0);
        RuleFor(x => x.ShippingAddress).NotEmpty();
    }

    public CommandResult<object> ValidateCommand(CreatePhysicalOrderCommand command)
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
