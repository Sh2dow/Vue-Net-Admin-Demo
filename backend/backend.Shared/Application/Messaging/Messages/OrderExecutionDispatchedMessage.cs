namespace backend.Shared.Application.Messaging.Messages;

public sealed record OrderExecutionDispatchedMessage(
    Guid OrderId,
    Guid PaymentId,
    DateTime DispatchedAtUtc
);
