using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Users.Requests.Users;

public sealed record DeleteUserCommand(Guid Id) : ICommand<Result<bool>>;
