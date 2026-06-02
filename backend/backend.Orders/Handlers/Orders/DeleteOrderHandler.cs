using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Orders.Requests.Orders;
using backend.Shared.Application.Results;
using backend.Shared.Application.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Orders.Handlers.Orders;

public sealed class DeleteOrderHandler : IRequestHandler<DeleteOrderCommand, Result<bool>>
{
    private readonly OrdersDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;

    public DeleteOrderHandler(OrdersDbContext db, IEffectiveUserAccessor effectiveUser)
    {
        _db = db;
        _effectiveUser = effectiveUser;
    }

    public async Task<Result<bool>> Handle(DeleteOrderCommand req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var order = await _db.Orders
            .FirstOrDefaultAsync(x => x.Id == req.Id && x.UserId == userId, ct);
        if (order == null) return Result<bool>.NotFound("Order not found.");

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
