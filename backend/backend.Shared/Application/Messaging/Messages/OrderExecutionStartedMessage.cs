namespace backend.Shared.Application.Messaging.Messages;

public sealed record OrderExecutionStartedMessage(
    Guid OrderId,
    Guid PaymentId,
    DateTime StartedAtUtc
);
