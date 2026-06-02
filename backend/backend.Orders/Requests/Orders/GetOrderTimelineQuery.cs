using System;
using System.Collections.Generic;
using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;

namespace backend.Orders.Requests.Orders;

public sealed record GetOrderTimelineQuery(Guid OrderId) : IQuery<IReadOnlyList<OrderTimelineItemDto>?>;
