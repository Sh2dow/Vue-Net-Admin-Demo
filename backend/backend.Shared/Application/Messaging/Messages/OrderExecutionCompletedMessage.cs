namespace backend.Shared.Application.Messaging.Messages;

public sealed record OrderExecutionCompletedMessage(
    Guid OrderId,
    Guid PaymentId,
    DateTime CompletedAtUtc
);
