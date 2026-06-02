namespace backend.Shared.Application.Messaging.Messages;

public sealed record PaymentAuthorizedMessage(
    Guid PaymentId,
    Guid OrderId,
    decimal TotalAmount,
    DateTime OccurredAtUtc
);
