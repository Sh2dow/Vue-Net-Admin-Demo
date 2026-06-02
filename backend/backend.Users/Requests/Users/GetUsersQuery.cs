using backend.Shared.Application.Abstractions;
using backend.Users.Dtos;

namespace backend.Users.Requests.Users;

public sealed record GetUsersQuery() : IQuery<IReadOnlyList<UserWithOrdersDto>>;
