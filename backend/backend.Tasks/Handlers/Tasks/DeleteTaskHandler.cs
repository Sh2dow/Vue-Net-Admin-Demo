using backend.Domain.Data;
using backend.Shared.Application.Results;
using backend.Shared.Application.Users;
using backend.Tasks.Requests.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Tasks.Handlers.Tasks;

public sealed class DeleteTaskHandler : IRequestHandler<DeleteTaskCommand, Result<bool>>
{
    private readonly TasksDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;

    public DeleteTaskHandler(TasksDbContext db, IEffectiveUserAccessor effectiveUser)
    {
        _db = db;
        _effectiveUser = effectiveUser;
    }

    public async Task<Result<bool>> Handle(DeleteTaskCommand req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var affected = await _db.Tasks
            .Where(x => x.Id == req.Id && x.UserId == userId)
            .ExecuteDeleteAsync(ct);

        return affected > 0
            ? Result<bool>.Success(true)
            : Result<bool>.NotFound("Task not found.");
    }
}
