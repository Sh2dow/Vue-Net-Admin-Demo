namespace backend.Domain.Models;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EventType { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAtUtc { get; set; }

    public int PublishAttempts { get; set; }

    public string? LastError { get; set; }
}
