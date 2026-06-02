using backend.Domain.Models;

namespace backend.Tests.Orders;

public class OrderTests
{
    [Fact]
    public void Create_Order_With_Valid_Data_Returns_Success()
    {
        var userId = Guid.NewGuid();
        var result = Order.Create(userId, 10m, "digital", "http://download", "");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(userId, result.Value!.UserId);
        Assert.Equal(10m, result.Value!.TotalAmount);
    }

    [Fact]
    public void Create_Order_With_Invalid_OrderType_Returns_Failure()
    {
        var result = Order.Create(Guid.NewGuid(), 10m, "", "http://download", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "validation" && e.Message.Contains("OrderType"));
    }

    [Fact]
    public void Create_Order_With_Negative_TotalAmount_Returns_Failure()
    {
        var result = Order.Create(Guid.NewGuid(), -1m, "digital", "http://download", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "validation" && e.Message.Contains("TotalAmount"));
    }

    [Fact]
    public void Create_Digital_Order_Without_DownloadUrl_Returns_Failure()
    {
        var result = Order.Create(Guid.NewGuid(), 10m, "digital", "", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "validation" && e.Message.Contains("DownloadUrl"));
    }

    [Fact]
    public void Create_Physical_Order_Without_ShippingAddress_Returns_Failure()
    {
        var result = Order.Create(Guid.NewGuid(), 10m, "physical", "", "");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "validation" && e.Message.Contains("ShippingAddress"));
    }

    [Fact]
    public void Update_Status_With_Invalid_Transition_Returns_Failure()
    {
        var userId = Guid.NewGuid();
        var orderResult = Order.Create(userId, 10m, "digital", "http://download", "");
        Assert.True(orderResult.IsSuccess);
        Assert.NotNull(orderResult.Value);
        var order = orderResult.Value!;

        var result = order.UpdateStatus("Completed", "test");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "domain" && e.Message.Contains("Completed"));
    }

    [Fact]
    public void Update_Status_With_Valid_Transition_Returns_Success()
    {
        var userId = Guid.NewGuid();
        var orderResult = Order.Create(userId, 10m, "digital", "http://download", "");
        Assert.True(orderResult.IsSuccess);
        Assert.NotNull(orderResult.Value);
        var order = orderResult.Value!;

        // Status transitions: Pending -> Paid -> Processing -> Completed
        var result1 = order.UpdateStatus("Paid", "payment received");
        Assert.True(result1.IsSuccess, $"Failed to update to Paid: {string.Join("; ", result1.Errors.Select(e => e.Message))}");

        var result2 = order.UpdateStatus("Processing", "processing order");
        Assert.True(result2.IsSuccess, $"Failed to update to Processing: {string.Join("; ", result2.Errors.Select(e => e.Message))}");

        var result3 = order.UpdateStatus("Completed", "order completed");
        Assert.True(result3.IsSuccess, $"Failed to update to Completed: {string.Join("; ", result3.Errors.Select(e => e.Message))}");
    }

    [Fact]
    public void Validate_Digital_Order_With_Valid_Data_Returns_Success()
    {
        var userId = Guid.NewGuid();
        var downloadUrl = "http://download";
        var digitalOrderResult = DigitalOrder.Create(userId, 10m, downloadUrl);
        Assert.True(digitalOrderResult.IsSuccess);
        Assert.NotNull(digitalOrderResult.Value);
        var digitalOrder = digitalOrderResult.Value!;
        var result = digitalOrder.ValidateDigitalOrder();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_Digital_Order_Without_DownloadUrl_Returns_Failure()
    {
        var userId = Guid.NewGuid();
        var digitalOrderResult = DigitalOrder.Create(userId, 10m, "http://download");
        Assert.True(digitalOrderResult.IsSuccess);
        Assert.NotNull(digitalOrderResult.Value);
        var digitalOrder = digitalOrderResult.Value!;
        digitalOrder.DownloadUrl = "";
        var result = digitalOrder.ValidateDigitalOrder();

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "domain" && e.Message.Contains("DownloadUrl"));
    }

    [Fact]
    public void Validate_Physical_Order_With_Valid_Data_Returns_Success()
    {
        var userId = Guid.NewGuid();
        var shippingAddress = "123 Main St";
        var physicalOrderResult = PhysicalOrder.Create(userId, 10m, shippingAddress);
        Assert.True(physicalOrderResult.IsSuccess);
        Assert.NotNull(physicalOrderResult.Value);
        var physicalOrder = physicalOrderResult.Value!;
        var result = physicalOrder.ValidatePhysicalOrder();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_Physical_Order_Without_ShippingAddress_Returns_Failure()
    {
        var userId = Guid.NewGuid();
        var physicalOrderResult = PhysicalOrder.Create(userId, 10m, "123 Main St");
        Assert.True(physicalOrderResult.IsSuccess);
        Assert.NotNull(physicalOrderResult.Value);
        var physicalOrder = physicalOrderResult.Value!;
        physicalOrder.ShippingAddress = "";
        var result = physicalOrder.ValidatePhysicalOrder();

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "domain" && e.Message.Contains("ShippingAddress"));
    }
}
