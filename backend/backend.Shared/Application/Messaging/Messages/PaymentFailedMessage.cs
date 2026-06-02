namespace backend.Shared.Application.Messaging.Messages;

public sealed record PaymentFailedMessage(
    Guid PaymentId,
    Guid OrderId,
    string Reason,
    DateTime OccurredAtUtc
);
