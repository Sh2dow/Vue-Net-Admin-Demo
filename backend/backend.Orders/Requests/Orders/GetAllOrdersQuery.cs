using System.Collections.Generic;
using backend.Shared.Application.Abstractions;

namespace backend.Orders.Requests.Orders;

public sealed record GetAllOrdersQuery : IQuery<IReadOnlyList<object>>;
