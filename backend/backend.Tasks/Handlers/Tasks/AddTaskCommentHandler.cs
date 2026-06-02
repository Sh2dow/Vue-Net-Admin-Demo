using backend.Domain.Data;
using backend.Domain.Models;
using backend.Shared.Application.Users;
using backend.Tasks.Dtos;
using backend.Tasks.Mappers;
using backend.Tasks.Requests.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Tasks.Handlers.Tasks;

public sealed class AddTaskCommentHandler : IRequestHandler<AddTaskCommentCommand, Shared.Application.Results.Result<TaskCommentDto>>
{
    private readonly TasksDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;
    private readonly IUserDirectory _userDirectory;

    public AddTaskCommentHandler(TasksDbContext db, IEffectiveUserAccessor effectiveUser, IUserDirectory userDirectory)
    {
        _db = db;
        _effectiveUser = effectiveUser;
        _userDirectory = userDirectory;
    }

    public async Task<Shared.Application.Results.Result<TaskCommentDto>> Handle(AddTaskCommentCommand req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var taskExists = await _db.Tasks
            .AnyAsync(x => x.Id == req.TaskId && x.UserId == userId, ct);
        if (!taskExists) return Shared.Application.Results.Result<TaskCommentDto>.NotFound("Task not found.");

        var comment = new TaskComment
        {
            TaskId = req.TaskId,
            AuthorId = userId,
            Content = req.Content.Trim()
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        var author = await _userDirectory.FindByIdAsync(userId, ct);
        var authorUsername = author?.Username ?? $"user-{userId.ToString("N")[..8]}";

        return Shared.Application.Results.Result<TaskCommentDto>.Success(comment.ToDto(authorUsername));
    }
}
