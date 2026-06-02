namespace backend.Domain.Models;

public class ConsumedMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Consumer { get; set; } = string.Empty;

    public string MessageId { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}
