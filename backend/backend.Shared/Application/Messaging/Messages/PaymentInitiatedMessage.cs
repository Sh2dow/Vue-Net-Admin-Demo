namespace backend.Shared.Application.Messaging.Messages;

public sealed record PaymentInitiatedMessage(
    Guid PaymentId,
    Guid OrderId,
    decimal TotalAmount,
    DateTime OccurredAtUtc
);
