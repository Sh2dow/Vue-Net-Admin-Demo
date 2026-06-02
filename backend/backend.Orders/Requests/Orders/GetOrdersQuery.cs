using System.Collections.Generic;
using backend.Orders.Dtos;
using backend.Shared.Application.Abstractions;

namespace backend.Orders.Requests.Orders;

public sealed record GetOrdersQuery(int PageNumber = 1, int PageSize = 20) : IQuery<IReadOnlyList<OrderViewDto>>;
