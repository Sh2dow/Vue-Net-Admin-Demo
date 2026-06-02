using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Orders.Requests.Orders;

public sealed record CreateDigitalOrderCommand(
    decimal TotalAmount,
    string DownloadUrl
) : ICommand<Result<OrderViewDto>>;
