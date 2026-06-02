namespace backend.Tasks.Dtos;

public sealed record TaskCommentDto(
    Guid Id,
    Guid AuthorId,
    string AuthorUsername,
    string Content,
    DateTime CreatedAtUtc
);

public sealed record TaskItemDto(
    Guid Id,
    Guid UserId,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<TaskCommentDto> Comments
);

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    string? Status,
    string? Priority
);

public sealed record UpdateTaskRequest(
    string? Title,
    string? Description,
    string? Status,
    string? Priority
);

public sealed record AddTaskCommentRequest(string Content);
