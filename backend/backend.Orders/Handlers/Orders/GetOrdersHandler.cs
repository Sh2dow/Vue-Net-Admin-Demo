using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Orders.Dtos;
using backend.Orders.Mappers;
using backend.Orders.Requests.Orders;
using backend.Shared.Application.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Orders.Handlers.Orders;

public sealed class GetOrdersHandler : IRequestHandler<GetOrdersQuery, IReadOnlyList<OrderViewDto>>
{
    private readonly OrdersDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;

    public GetOrdersHandler(OrdersDbContext db, IEffectiveUserAccessor effectiveUser)
    {
        _db = db;
        _effectiveUser = effectiveUser;
    }

    public async Task<IReadOnlyList<OrderViewDto>> Handle(GetOrdersQuery req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var pageNumber = Math.Max(req.PageNumber, 1);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return orders.Select(OrderMapper.ToDto).ToList();
    }
}
