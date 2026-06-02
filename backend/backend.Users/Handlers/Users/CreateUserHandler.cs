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

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<UserWithOrdersDto>>
{
    private readonly IUserDirectory _userDirectory;
    private readonly IIntegrationEventOutbox _outbox;

    public CreateUserHandler(IUserDirectory userDirectory, IIntegrationEventOutbox outbox)
    {
        _userDirectory = userDirectory;
        _outbox = outbox;
    }

    public async Task<backend.Shared.Application.Results.Result<UserWithOrdersDto>> Handle(CreateUserCommand req, CancellationToken ct)
    {
        var subject = req.Subject.Trim();
        var existing = await _userDirectory.FindBySubjectAsync(subject, ct);
        if (existing != null) return backend.Shared.Application.Results.Result<UserWithOrdersDto>.Conflict("User with this subject already exists.");

        var user = req.ToEntity();
        user.Subject = subject;
        user.Username = req.Username.Trim();
        user.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();

        user = await _userDirectory.CreateAsync(user, ct);

        // Publish user created event
        await _outbox.EnqueueAsync(
            "user.created",
            new UserCreatedMessage(user.Id, user.Username, user.Email, DateTime.UtcNow),
            user.Id.ToString(),
            ct);

        return backend.Shared.Application.Results.Result<UserWithOrdersDto>.Success(user.ToDto([]));
    }
}
