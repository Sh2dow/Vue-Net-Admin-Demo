namespace backend.Domain.Models;

public class PhysicalOrder : Order
{
    public string ShippingAddress { get; set; } = string.Empty;

    public string? TrackingNumber { get; set; }

    public PhysicalOrder()
    {
    }

    protected PhysicalOrder(Guid id, Guid userId, decimal totalAmount, string shippingAddress)
    {
        Id = id;
        UserId = userId;
        TotalAmount = totalAmount;
        ShippingAddress = shippingAddress;
    }

    public static DomainResult<PhysicalOrder> Create(Guid userId, decimal totalAmount, string shippingAddress)
    {
        var baseResult = Order.Create(userId, totalAmount, "physical", downloadUrl: null, shippingAddress: shippingAddress);
        
        if (!baseResult.IsSuccess)
            return DomainResult<PhysicalOrder>.Failure(baseResult.Errors);
        
        // For now, create a new PhysicalOrder instance with the validated data
        // Note: The base factory currently creates a generic Order
        return DomainResult<PhysicalOrder>.Success(new PhysicalOrder
        {
            UserId = userId,
            TotalAmount = totalAmount,
            ShippingAddress = shippingAddress
        });
    }
}
