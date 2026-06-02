using System;
using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Orders.Requests.Orders;

public sealed record RetryOrderPaymentCommand(Guid OrderId) : ICommand<Result<OrderViewDto>>;
