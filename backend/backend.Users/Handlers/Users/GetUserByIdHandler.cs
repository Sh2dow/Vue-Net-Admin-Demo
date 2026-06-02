using backend.Domain.Data;
using backend.Shared.Application.Users;
using backend.Users.Dtos;
using backend.Users.Mappers;
using backend.Users.Requests.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Users.Handlers.Users;

public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, UserWithOrdersDto?>
{
    private readonly IUserDirectory _userDirectory;

    public GetUserByIdHandler(IUserDirectory userDirectory)
    {
        _userDirectory = userDirectory;
    }

    public async Task<UserWithOrdersDto?> Handle(GetUserByIdQuery req, CancellationToken ct)
    {
        var user = await _userDirectory.FindByIdAsync(req.Id, ct);
        if (user == null)
        {
            return null;
        }

        // In a real microservices architecture, orders would be fetched via HTTP call to orders-api
        // For now, return user without orders
        return user.ToDto([]);
    }
}
