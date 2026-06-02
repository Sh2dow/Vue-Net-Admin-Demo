namespace backend.Domain.Models;

public abstract class OrderEvent
{
    protected OrderEvent(Guid orderId, DateTime occurredAtUtc)
    {
        OrderId = orderId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid OrderId { get; }

    public DateTime OccurredAtUtc { get; }
}
