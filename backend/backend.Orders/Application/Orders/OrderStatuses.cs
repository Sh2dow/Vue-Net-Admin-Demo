namespace backend.Orders.Application.Orders;

public static class OrderStatuses
{
    public const string PaymentPending = "PaymentPending";
    public const string PaymentAuthorized = "PaymentAuthorized";
    public const string PaymentFailed = "PaymentFailed";
    public const string ExecutionDispatched = "ExecutionDispatched";
    public const string ExecutionStarted = "ExecutionStarted";
    public const string ExecutionCompleted = "ExecutionCompleted";
    public const string ExecutionFailed = "ExecutionFailed";
}
