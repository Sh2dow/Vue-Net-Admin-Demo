using backend.Shared.Application.Messaging;
using backend.Shared.Application.Results;
using backend.Shared.Application.Users;
using backend.Users.Requests.Users;
using MediatR;

namespace backend.Users.Handlers.Users;

public sealed class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Result<bool>>
{
    private readonly IUserDirectory _userDirectory;
    private readonly IIntegrationEventOutbox _outbox;

    public DeleteUserHandler(IUserDirectory userDirectory, IIntegrationEventOutbox outbox)
    {
        _userDirectory = userDirectory;
        _outbox = outbox;
    }

    public async Task<backend.Shared.Application.Results.Result<bool>> Handle(DeleteUserCommand req, CancellationToken ct)
    {
        var user = await _userDirectory.FindByIdAsync(req.Id, ct);
        if (user == null) return backend.Shared.Application.Results.Result<bool>.NotFound("User not found.");

        var affected = await _userDirectory.DeleteByIdAsync(req.Id, ct);

        if (affected > 0)
        {
            // Publish user deleted event
            await _outbox.EnqueueAsync(
                "user.deleted",
                new UserDeletedMessage(user.Id, DateTime.UtcNow),
                user.Id.ToString(),
                ct);
        }

        return affected > 0
            ? backend.Shared.Application.Results.Result<bool>.Success(true)
            : backend.Shared.Application.Results.Result<bool>.NotFound("User not found.");
    }
}
