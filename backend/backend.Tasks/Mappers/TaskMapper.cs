using backend.Domain.Models;
using backend.Tasks.Dtos;
using backend.Tasks.Requests.Tasks;
using Riok.Mapperly.Abstractions;

namespace backend.Tasks.Mappers;

[Mapper]
public static partial class TaskMapper
{
    public static TaskCommentDto ToDto(this TaskComment comment, string authorUsername) =>
        new(
            comment.Id,
            comment.AuthorId,
            authorUsername,
            comment.Content,
            comment.CreatedAtUtc
        );

    public static TaskItemDto ToDto(this TaskItem task, IReadOnlyList<TaskCommentDto> comments) =>
        new(
            task.Id,
            task.UserId,
            task.Title,
            task.Description,
            string.IsNullOrWhiteSpace(task.Status) ? "todo" : task.Status,
            string.IsNullOrWhiteSpace(task.Priority) ? "medium" : task.Priority,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            comments
        );

    public static TaskItemDto ToDto(this TaskItem task, IReadOnlyDictionary<Guid, AppUser> usersById)
    {
        var comments = task.Comments
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(comment => comment.ToDto(ResolveUsername(usersById, comment.AuthorId)))
            .ToList();

        return task.ToDto(comments);
    }

    [MapperIgnoreTarget(nameof(TaskItem.Id))]
    [MapperIgnoreTarget(nameof(TaskItem.UserId))]
    [MapperIgnoreTarget(nameof(TaskItem.CreatedAtUtc))]
    [MapperIgnoreTarget(nameof(TaskItem.UpdatedAtUtc))]
    [MapperIgnoreTarget(nameof(TaskItem.Comments))]
    public static partial TaskItem ToEntity(this CreateTaskCommand command);

    private static string ResolveUsername(IReadOnlyDictionary<Guid, AppUser> usersById, Guid userId)
    {
        return usersById.TryGetValue(userId, out var user)
            ? user.Username
            : $"user-{userId.ToString("N")[..8]}";
    }
}
