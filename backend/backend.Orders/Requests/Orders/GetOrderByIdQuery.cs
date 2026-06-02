using System;
using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;

namespace backend.Orders.Requests.Orders;

public sealed record GetOrderByIdQuery(Guid Id) : IQuery<OrderViewDto?>;
