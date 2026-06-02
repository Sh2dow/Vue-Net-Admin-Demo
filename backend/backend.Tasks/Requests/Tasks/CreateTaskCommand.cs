using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;
using backend.Tasks.Dtos;

namespace backend.Tasks.Requests.Tasks;

public sealed record CreateTaskCommand(
    string Title,
    string? Description,
    string? Status,
    string? Priority
) : ICommand<Result<TaskItemDto>>;
