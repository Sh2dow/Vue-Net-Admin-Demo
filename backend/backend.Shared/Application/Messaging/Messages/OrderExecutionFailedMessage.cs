namespace backend.Shared.Application.Messaging.Messages;

public sealed record OrderExecutionFailedMessage(
    Guid OrderId,
    Guid PaymentId,
    string Reason,
    DateTime FailedAtUtc
);
