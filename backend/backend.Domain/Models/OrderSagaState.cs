namespace backend.Domain.Models;

public class OrderSagaState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Guid? PaymentId { get; set; }

    public string State { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastPaymentRequestedAtUtc { get; set; }

    public DateTime? LastPaymentCompletedAtUtc { get; set; }

    public DateTime? ExecutionDispatchedAtUtc { get; set; }

    public DateTime? ExecutionStartedAtUtc { get; set; }

    public DateTime? ExecutionCompletedAtUtc { get; set; }

    public DateTime? ExecutionFailedAtUtc { get; set; }

    public string? ExecutionFailureReason { get; set; }
}
