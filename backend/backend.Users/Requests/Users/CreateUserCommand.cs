using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;
using backend.Users.Dtos;

namespace backend.Users.Requests.Users;

public sealed record CreateUserCommand(
    string Subject,
    string Username,
    string? Email
) : ICommand<Result<UserWithOrdersDto>>;
