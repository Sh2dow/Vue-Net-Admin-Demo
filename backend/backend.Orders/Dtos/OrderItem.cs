using System;

namespace backend.Orders.Dtos;

public sealed record OrderItem(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice
);
