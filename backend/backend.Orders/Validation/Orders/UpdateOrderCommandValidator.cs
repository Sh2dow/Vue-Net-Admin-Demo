using System;
using System.Linq;
using backend.Domain.Models;
using backend.Orders.Requests.Orders;
using FluentValidation;

namespace backend.Orders.Validation.Orders;

public sealed class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.TotalAmount).GreaterThan(0);
        RuleFor(x => x.Status).NotEmpty();
    }

    public CommandResult<object> ValidateCommand(UpdateOrderCommand command)
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
