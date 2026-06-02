namespace backend.Domain.Models;

public class PaymentEventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Guid OrderId { get; set; }

    public int AttemptNumber { get; set; } = 1;

    public int SequenceNumber { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Data { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
