using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Orders.Requests.Orders;

public sealed record CreateOrderCommand(
    string OrderType,
    decimal TotalAmount,
    string? DownloadUrl,
    string? ShippingAddress,
    string? TrackingNumber
) : ICommand<Result<OrderViewDto>>;
