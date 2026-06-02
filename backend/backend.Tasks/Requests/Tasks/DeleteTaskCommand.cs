using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Tasks.Requests.Tasks;

public sealed record DeleteTaskCommand(Guid Id) : ICommand<Result<bool>>;
