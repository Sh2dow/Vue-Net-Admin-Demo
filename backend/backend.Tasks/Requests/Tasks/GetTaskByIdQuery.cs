using backend.Shared.Application.Abstractions;
using backend.Tasks.Dtos;

namespace backend.Tasks.Requests.Tasks;

public sealed record GetTaskByIdQuery(Guid Id) : IQuery<TaskItemDto?>;
