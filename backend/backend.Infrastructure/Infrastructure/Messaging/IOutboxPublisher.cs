namespace backend.Infrastructure.Infrastructure.Messaging;

public interface IOutboxPublisher
{
    Task PublishAsync(string routingKey, ReadOnlyMemory<byte> body, string? eventType, string? correlationId, string? messageId, CancellationToken ct = default);

    Task EnsureInitializedAsync(CancellationToken ct = default);
}
