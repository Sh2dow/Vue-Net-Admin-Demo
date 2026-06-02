using backend.Shared.Application.Abstractions;
using backend.Tasks.Dtos;

namespace backend.Tasks.Requests.Tasks;

public sealed record GetTasksQuery(int PageNumber = 1, int PageSize = 20) : IQuery<IReadOnlyList<TaskItemDto>>;
