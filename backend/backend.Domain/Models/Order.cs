namespace backend.Domain.Models;

public class Order
{
    private string _status = "Pending";
    private readonly List<OrderEvent> _events = [];

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status
    {
        get => _status;
        set => _status = value;
    }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<OrderEvent> Events => _events;

    protected Order()
    {
    }

    protected Order(Guid id, Guid userId, decimal totalAmount, string status)
    {
        Id = id;
        UserId = userId;
        TotalAmount = totalAmount;
        _status = status;
    }

    public static DomainResult<Order> Create(
        Guid userId,
        decimal totalAmount,
        string orderType,
        string? downloadUrl = null,
        string? shippingAddress = null)
    {
        var errors = new List<ResultError>();

        if (string.IsNullOrWhiteSpace(orderType))
            errors.Add(new("validation", "OrderType is required", nameof(orderType)));

        if (totalAmount <= 0)
            errors.Add(new("validation", "TotalAmount must be positive", nameof(totalAmount)));

        if (orderType == "digital" && string.IsNullOrWhiteSpace(downloadUrl))
            errors.Add(new("validation", "DownloadUrl required for digital orders", nameof(downloadUrl)));

        if (orderType == "physical" && string.IsNullOrWhiteSpace(shippingAddress))
            errors.Add(new("validation", "ShippingAddress required for physical orders", nameof(shippingAddress)));

        if (errors.Any())
            return DomainResult<Order>.Failure(errors);

        Order order;
        if (orderType == "digital")
        {
            order = new DigitalOrder { UserId = userId, TotalAmount = totalAmount, DownloadUrl = downloadUrl! };
        }
        else if (orderType == "physical")
        {
            order = new PhysicalOrder { UserId = userId, TotalAmount = totalAmount, ShippingAddress = shippingAddress! };
        }
        else
        {
            throw new InvalidOperationException($"Unsupported order type: {orderType}");
        }

        order._status = "Pending";

        return DomainResult<Order>.Success(order);
    }

    public DomainResult<DomainUnit> UpdateStatus(string newStatus, string reason)
    {
        var currentStatus = Status;

        // Domain rules for valid transitions
        if (newStatus == "Completed" && currentStatus != "Paid" && currentStatus != "Processing")
            return DomainResult<DomainUnit>.Failure(new ResultError(
                "domain",
                $"Cannot transition to Completed from {currentStatus}",
                nameof(newStatus)));

        if (newStatus == "Paid" && currentStatus != "Pending" && currentStatus != "Processing")
            return DomainResult<DomainUnit>.Failure(new ResultError(
                "domain",
                $"Cannot transition to Paid from {currentStatus}",
                nameof(newStatus)));

        // Raise domain event
        _events.Add(new OrderStatusChanged(Id, newStatus, reason));
        _status = newStatus;

        return DomainResult<DomainUnit>.Success(new DomainUnit());
    }

    public DomainResult<DomainUnit> Validate()
    {
        var errors = new List<ResultError>();

        if (TotalAmount <= 0)
            errors.Add(new("domain", "TotalAmount must be positive", nameof(TotalAmount)));

        if (string.IsNullOrWhiteSpace(_status))
            errors.Add(new("domain", "Status is required", nameof(Status)));

        if (errors.Any())
            return DomainResult<DomainUnit>.Failure(errors);

        return DomainResult<DomainUnit>.Success(new DomainUnit());
    }

    public DomainResult<DomainUnit> ValidateDigitalOrder()
    {
        var validateResult = Validate();
        if (!validateResult.IsSuccess)
            return DomainResult<DomainUnit>.Failure(validateResult.Errors);

        var errors = new List<ResultError>();

        if (this is DigitalOrder digitalOrder && string.IsNullOrWhiteSpace(digitalOrder.DownloadUrl))
            errors.Add(new("domain", "DownloadUrl is required for digital orders", nameof(DigitalOrder.DownloadUrl)));

        if (errors.Any())
            return DomainResult<DomainUnit>.Failure(errors);

        return DomainResult<DomainUnit>.Success(new DomainUnit());
    }

    public DomainResult<DomainUnit> ValidatePhysicalOrder()
    {
        var validateResult = Validate();
        if (!validateResult.IsSuccess)
            return DomainResult<DomainUnit>.Failure(validateResult.Errors);

        var errors = new List<ResultError>();

        if (this is PhysicalOrder physicalOrder && string.IsNullOrWhiteSpace(physicalOrder.ShippingAddress))
            errors.Add(new("domain", "ShippingAddress is required for physical orders", nameof(PhysicalOrder.ShippingAddress)));

        if (errors.Any())
            return DomainResult<DomainUnit>.Failure(errors);

        return DomainResult<DomainUnit>.Success(new DomainUnit());
    }
}
