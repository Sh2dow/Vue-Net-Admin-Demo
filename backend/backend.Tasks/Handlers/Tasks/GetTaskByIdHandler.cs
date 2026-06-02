using backend.Domain.Data;
using backend.Shared.Application.Users;
using backend.Tasks.Dtos;
using backend.Tasks.Mappers;
using backend.Tasks.Requests.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Tasks.Handlers.Tasks;

public sealed class GetTaskByIdHandler : IRequestHandler<GetTaskByIdQuery, TaskItemDto?>
{
    private readonly TasksDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;
    private readonly IUserDirectory _userDirectory;

    public GetTaskByIdHandler(TasksDbContext db, IEffectiveUserAccessor effectiveUser, IUserDirectory userDirectory)
    {
        _db = db;
        _effectiveUser = effectiveUser;
        _userDirectory = userDirectory;
    }

    public async Task<TaskItemDto?> Handle(GetTaskByIdQuery req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var task = await _db.Tasks
            .AsNoTracking()
            .Include(x => x.Comments)
            .FirstOrDefaultAsync(x => x.Id == req.Id && x.UserId == userId, ct);

        if (task == null)
        {
            return null;
        }

        var usersById = await _userDirectory.GetByIdsAsync(task.Comments.Select(x => x.AuthorId), ct);

        return task.ToDto(usersById);
    }
}
