namespace backend.Shared.Application.Messaging;

public sealed record IntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    string RoutingKey,
    DateTime OccurredAtUtc,
    string Payload,
    string? CorrelationId
);
