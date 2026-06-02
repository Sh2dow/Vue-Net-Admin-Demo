namespace backend.Shared.Application.Messaging.Messages;

public sealed record OrderPaymentRequestedMessage(
    Guid OrderId,
    Guid UserId,
    string OrderType,
    decimal TotalAmount,
    DateTime RequestedAtUtc
);
