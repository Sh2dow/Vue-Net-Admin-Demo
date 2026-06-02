using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Orders.Requests.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Orders.Handlers.Orders;

public sealed class GetAllOrdersHandler : IRequestHandler<GetAllOrdersQuery, IReadOnlyList<object>>
{
    private readonly OrdersDbContext _db;

    public GetAllOrdersHandler(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<object>> Handle(GetAllOrdersQuery req, CancellationToken ct)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                OrderType = x is backend.Domain.Models.DigitalOrder ? "digital" : (x is backend.Domain.Models.PhysicalOrder ? "physical" : "unknown"),
                x.TotalAmount,
                x.Status,
                x.CreatedAtUtc
            })
            .ToListAsync(ct);

        return orders.Cast<object>().ToList();
    }
}
