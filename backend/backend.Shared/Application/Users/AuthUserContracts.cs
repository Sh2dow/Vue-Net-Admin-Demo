namespace backend.Shared.Application.Users;

public sealed record AuthUserDto(
    Guid Id,
    string Subject,
    string Username,
    string? Email,
    DateTime CreatedAtUtc
);

public sealed record EnsureAuthUserRequest(
    string Subject,
    string? PreferredUsername,
    string? Email
);

public sealed record CreateAuthUserRequest(
    string Subject,
    string Username,
    string? Email
);

public sealed record UpdateAuthUserRequest(
    string Username,
    string? Email
);

public sealed record UserCreatedMessage(
    Guid UserId,
    string Username,
    string? Email,
    DateTime OccurredAtUtc
);

public sealed record UserUpdatedMessage(
    Guid UserId,
    string Username,
    string? Email,
    DateTime OccurredAtUtc
);

public sealed record UserDeletedMessage(
    Guid UserId,
    DateTime OccurredAtUtc
);
