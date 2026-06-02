using System;
using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;

namespace backend.Orders.Requests.Orders;

public sealed record GetOrderWorkflowQuery(Guid OrderId) : IQuery<OrderWorkflowDto?>;
