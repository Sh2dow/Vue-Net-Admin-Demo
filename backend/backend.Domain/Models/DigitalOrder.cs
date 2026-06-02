namespace backend.Domain.Models;

public class DigitalOrder : Order
{
    public string DownloadUrl { get; set; } = string.Empty;

    public DigitalOrder()
    {
    }

    protected DigitalOrder(Guid id, Guid userId, decimal totalAmount, string downloadUrl)
    {
        Id = id;
        UserId = userId;
        TotalAmount = totalAmount;
        DownloadUrl = downloadUrl;
    }

    public static DomainResult<DigitalOrder> Create(Guid userId, decimal totalAmount, string downloadUrl)
    {
        var baseResult = Order.Create(userId, totalAmount, "digital", downloadUrl: downloadUrl, shippingAddress: null);
        
        if (!baseResult.IsSuccess)
            return DomainResult<DigitalOrder>.Failure(baseResult.Errors);
        
        // For now, create a new DigitalOrder instance with the validated data
        // Note: The base factory currently creates a generic Order
        return DomainResult<DigitalOrder>.Success(new DigitalOrder
        {
            UserId = userId,
            TotalAmount = totalAmount,
            DownloadUrl = downloadUrl
        });
    }
}
