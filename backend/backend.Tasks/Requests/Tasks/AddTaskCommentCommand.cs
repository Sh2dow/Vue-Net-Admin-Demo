using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;
using backend.Tasks.Dtos;

namespace backend.Tasks.Requests.Tasks;

public sealed record AddTaskCommentCommand(Guid TaskId, string Content) : ICommand<Result<TaskCommentDto>>;
