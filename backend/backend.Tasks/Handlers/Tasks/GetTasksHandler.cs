using backend.Domain.Data;
using backend.Shared.Application.Users;
using backend.Tasks.Dtos;
using backend.Tasks.Mappers;
using backend.Tasks.Requests.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Tasks.Handlers.Tasks;

public sealed class GetTasksHandler : IRequestHandler<GetTasksQuery, IReadOnlyList<TaskItemDto>>
{
    private readonly TasksDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;
    private readonly IUserDirectory _userDirectory;

    public GetTasksHandler(TasksDbContext db, IEffectiveUserAccessor effectiveUser, IUserDirectory userDirectory)
    {
        _db = db;
        _effectiveUser = effectiveUser;
        _userDirectory = userDirectory;
    }

    public async Task<IReadOnlyList<TaskItemDto>> Handle(GetTasksQuery req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var pageNumber = Math.Max(req.PageNumber, 1);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        var tasks = await _db.Tasks
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Comments)
            .ToListAsync(ct);

        var usersById = await _userDirectory.GetByIdsAsync(
            tasks.SelectMany(x => x.Comments).Select(x => x.AuthorId),
            ct);

        return tasks.Select(task => task.ToDto(usersById)).ToList();
    }
}
