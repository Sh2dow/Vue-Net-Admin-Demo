using backend.Domain.Data;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Results;
using backend.Shared.Application.Users;
using backend.Users.Dtos;
using backend.Users.Mappers;
using backend.Users.Requests.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Users.Handlers.Users;

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserCommand, Result<UserWithOrdersDto>>
{
    private readonly IUserDirectory _userDirectory;
    private readonly IIntegrationEventOutbox _outbox;

    public UpdateUserHandler(IUserDirectory userDirectory, IIntegrationEventOutbox outbox)
    {
        _userDirectory = userDirectory;
        _outbox = outbox;
    }

    public async Task<backend.Shared.Application.Results.Result<UserWithOrdersDto>> Handle(UpdateUserCommand req, CancellationToken ct)
    {
        var user = await _userDirectory.FindByIdAsync(req.Id, ct);
        if (user == null) return backend.Shared.Application.Results.Result<UserWithOrdersDto>.NotFound("User not found.");

        var originalUsername = user.Username;
        var originalEmail = user.Email;

        user.Username = req.Username.Trim();
        user.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        user = await _userDirectory.UpdateAsync(user, ct);

        // Publish user updated event if username or email changed
        if (originalUsername != user.Username || originalEmail != user.Email)
        {
            await _outbox.EnqueueAsync(
                "user.updated",
                new UserUpdatedMessage(user.Id, user.Username, user.Email, DateTime.UtcNow),
                user.Id.ToString(),
                ct);
        }

        // In a real microservices architecture, orders would be fetched via HTTP call to orders-api
        // For now, return user without orders
        return backend.Shared.Application.Results.Result<UserWithOrdersDto>.Success(user.ToDto([]));
    }
}
