using System;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Orders.Requests.Orders;

public sealed record DeleteOrderCommand(Guid Id) : ICommand<Result<bool>>;
