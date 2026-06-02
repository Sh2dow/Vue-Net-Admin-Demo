namespace backend.Orders.Infrastructure.Orders;

public sealed class OrderExecutionOptions
{
    public const string SectionName = "OrderExecution";

    public bool AutoComplete { get; set; } = true;
    public int StubDelayMilliseconds { get; set; } = 250;
}
