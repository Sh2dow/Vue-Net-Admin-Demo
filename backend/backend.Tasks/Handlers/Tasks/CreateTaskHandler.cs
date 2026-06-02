using backend.Domain.Data;
using backend.Shared.Application.Results;
using backend.Shared.Application.Users;
using backend.Tasks.Dtos;
using backend.Tasks.Mappers;
using backend.Tasks.Requests.Tasks;
using MediatR;

namespace backend.Tasks.Handlers.Tasks;

public sealed class CreateTaskHandler : IRequestHandler<CreateTaskCommand, Result<TaskItemDto>>
{
    private readonly TasksDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;

    public CreateTaskHandler(TasksDbContext db, IEffectiveUserAccessor effectiveUser)
    {
        _db = db;
        _effectiveUser = effectiveUser;
    }

    public async Task<Result<TaskItemDto>> Handle(CreateTaskCommand req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var task = req.ToEntity();
        task.UserId = userId;
        task.Title = req.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        task.Status = NormalizeStatus(req.Status);
        task.Priority = NormalizePriority(req.Priority);

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        return Result<TaskItemDto>.Success(task.ToDto([]));
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "todo" => "todo",
            "in-progress" => "in-progress",
            "done" => "done",
            _ => "todo"
        };
    }

    private static string NormalizePriority(string? priority)
    {
        var normalized = priority?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            _ => "medium"
        };
    }
}
