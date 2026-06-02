using backend.Shared.Application.Abstractions;

namespace backend.Tasks.Requests.Tasks;

public sealed record DebugRolesQuery(IReadOnlyList<string> Roles) : IQuery<IReadOnlyList<string>>;
