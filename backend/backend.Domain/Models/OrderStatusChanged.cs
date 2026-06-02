namespace backend.Domain.Models;

public sealed class OrderStatusChanged : OrderEvent
{
    public string NewStatus { get; }
    public string Reason { get; }

    public OrderStatusChanged(Guid orderId, string newStatus, string reason)
        : base(orderId, DateTime.UtcNow)
    {
        NewStatus = newStatus;
        Reason = reason;
    }
}
