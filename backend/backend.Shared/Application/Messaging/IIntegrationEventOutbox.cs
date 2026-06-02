namespace backend.Shared.Application.Messaging;

public interface IIntegrationEventOutbox
{
    Task EnqueueAsync<T>(
        string routingKey,
        T message,
        string? correlationId = null,
        CancellationToken ct = default);
}
