namespace backend.Domain.Models;

public abstract record UserEvent(Guid UserId, DateTime OccurredAtUtc);

public sealed record UserCreatedEvent(
    Guid UserId,
    string Username,
    string? Email,
    DateTime OccurredAtUtc
) : UserEvent(UserId, OccurredAtUtc);

public sealed record UserUpdatedEvent(
    Guid UserId,
    string Username,
    string? Email,
    DateTime OccurredAtUtc
) : UserEvent(UserId, OccurredAtUtc);

public sealed record UserDeletedEvent(
    Guid UserId,
    DateTime OccurredAtUtc
) : UserEvent(UserId, OccurredAtUtc);
