using System;

namespace backend.Orders.Dtos;

public sealed record OrderViewDto(
    Guid Id,
    string OrderType,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc,
    string? DownloadUrl,
    string? ShippingAddress,
    string? TrackingNumber
)
{
    public OrderDto ToDto() => new(Id, Guid.Empty, OrderType, TotalAmount, Status, CreatedAtUtc, DownloadUrl, ShippingAddress, TrackingNumber);
}
