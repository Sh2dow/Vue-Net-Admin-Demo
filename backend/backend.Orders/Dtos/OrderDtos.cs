namespace backend.Orders.Dtos;

public sealed record CreateDigitalOrderRequest(decimal TotalAmount, string DownloadUrl);

public sealed record CreatePhysicalOrderRequest(decimal TotalAmount, string ShippingAddress, string? TrackingNumber);

public sealed record CreateOrderRequest(
    string OrderType,
    decimal TotalAmount,
    string? DownloadUrl,
    string? ShippingAddress,
    string? TrackingNumber
);

public sealed record UpdateOrderRequest(
    string OrderNumber,
    decimal TotalAmount
);

public sealed record OrderDto(
    Guid Id,
    Guid UserId,
    string OrderType,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc,
    string? DownloadUrl,
    string? ShippingAddress,
    string? TrackingNumber
);
