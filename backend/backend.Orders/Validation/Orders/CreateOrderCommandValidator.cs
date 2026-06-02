using System;
using backend.Orders.Requests.Orders;
using FluentValidation;

namespace backend.Orders.Validation.Orders;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.OrderType)
            .NotEmpty()
            .Must(orderType =>
                !string.IsNullOrWhiteSpace(orderType)
                && (orderType.Trim().Equals("digital", StringComparison.OrdinalIgnoreCase)
                    || orderType.Trim().Equals("physical", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("OrderType must be either 'digital' or 'physical'.");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0);

        When(x => string.Equals(x.OrderType?.Trim(), "digital", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.DownloadUrl)
                .NotEmpty()
                .WithMessage("DownloadUrl is required for digital orders.");
        });

        When(x => string.Equals(x.OrderType?.Trim(), "physical", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.ShippingAddress)
                .NotEmpty()
                .WithMessage("ShippingAddress is required for physical orders.");
        });
    }
}
