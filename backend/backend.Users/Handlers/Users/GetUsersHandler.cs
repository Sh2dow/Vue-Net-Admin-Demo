using backend.Domain.Data;
using backend.Domain.Models;
using backend.Shared.Application.Users;
using backend.Users.Dtos;
using backend.Users.Mappers;
using backend.Users.Requests.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Users.Handlers.Users;

public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserWithOrdersDto>>
{
    private readonly IUserDirectory _userDirectory;
    private readonly OrdersDbContext? _ordersDb;

    public GetUsersHandler(IUserDirectory userDirectory, OrdersDbContext? ordersDb = null)
    {
        _userDirectory = userDirectory;
        _ordersDb = ordersDb;
    }

    public async Task<IReadOnlyList<UserWithOrdersDto>> Handle(GetUsersQuery req, CancellationToken ct)
    {
        var users = await _userDirectory.ListAsync(ct);
        
        if (_ordersDb == null)
        {
            return users.Select(user => user.ToDto([])).ToList();
        }

        var userIds = users.Select(u => u.Id).ToList();
        var orders = await _ordersDb.Orders
            .AsNoTracking()
            .Where(o => userIds.Contains(o.UserId))
            .ToListAsync(ct);

        var ordersByUser = orders.GroupBy(o => o.UserId).ToDictionary(g => g.Key, g => g.ToList());

        return users.Select(user => 
        {
            var userOrders = ordersByUser.TryGetValue(user.Id, out var userOrderList) 
                ? userOrderList 
                : [];
            return user.ToDto(userOrders);
        }).ToList();
    }
}
