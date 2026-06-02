namespace backend.Users.Dtos;

public sealed record CreateUserRequest(string Subject, string Username, string? Email);

public sealed record UpdateUserRequest(string Username, string? Email);

public sealed record UserOrderSummaryDto(
    Guid Id,
    string OrderType,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc,
    string? DownloadUrl,
    string? ShippingAddress,
    string? TrackingNumber
);

public sealed record UserDto(
    Guid Id,
    string Subject,
    string Username,
    string? Email,
    DateTime CreatedAtUtc,
    IReadOnlyList<UserOrderSummaryDto> Orders
);

public sealed record UserWithOrdersDto(
    Guid Id,
    string Subject,
    string Username,
    string? Email,
    DateTime CreatedAtUtc,
    IReadOnlyList<UserOrderSummaryDto> Orders
)
{
    public UserDto ToDto() => new(Id, Subject, Username, Email, CreatedAtUtc, Orders);
}
