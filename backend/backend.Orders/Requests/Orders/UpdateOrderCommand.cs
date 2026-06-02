using System;
using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Orders.Requests.Orders;

public sealed record UpdateOrderCommand(
    Guid Id,
    decimal TotalAmount,
    string Status,
    string? DownloadUrl,
    string? ShippingAddress,
    string? TrackingNumber
) : ICommand<Result<OrderViewDto>>;
