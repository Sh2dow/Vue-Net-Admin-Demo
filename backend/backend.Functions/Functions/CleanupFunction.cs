using backend.Domain.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Functions.Functions;

public class CleanupFunction
{
    private readonly TasksDbContext _db;
    private readonly ILogger<CleanupFunction> _logger;

    public CleanupFunction(TasksDbContext db, ILogger<CleanupFunction> logger)
    {
        _db = db;
        _logger = logger;
    }

    [Function("DailyCleanup")]
    public async Task Run(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo,
        CancellationToken ct)
    {
        _logger.LogInformation("Daily cleanup started at {Time}", DateTime.UtcNow);

        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var deletedTasks = await _db.Tasks
            .Where(x => x.Status == "done" && x.UpdatedAtUtc < cutoffDate)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "Daily cleanup completed. Deleted {DeletedCount} completed tasks older than {CutoffDate}.",
            deletedTasks, cutoffDate);
    }
}
