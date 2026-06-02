using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Orders.Requests.Orders;

public sealed record CreatePhysicalOrderCommand(
    decimal TotalAmount,
    string ShippingAddress,
    string? TrackingNumber
) : ICommand<Result<OrderViewDto>>;
