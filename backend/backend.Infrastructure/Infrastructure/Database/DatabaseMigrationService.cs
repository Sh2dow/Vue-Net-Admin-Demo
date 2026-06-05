using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace backend.Infrastructure.Infrastructure.Database;

public sealed class DatabaseMigrationService<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseMigrationService<TDbContext>> _logger;

    public DatabaseMigrationService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseMigrationService<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        var maxDelay = TimeSpan.FromSeconds(30);
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                await db.Database.MigrateAsync(stoppingToken);
                _logger.LogInformation("Migrations applied for {DbContext}", typeof(TDbContext).Name);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Migration cancelled for {DbContext}", typeof(TDbContext).Name);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration attempt {Attempt} for {DbContext} failed.",
                    attempt, typeof(TDbContext).Name);

                if (attempt >= 10)
                {
                    _logger.LogCritical(
                        "Exhausted 10 retries for {DbContext}. App may be degraded.",
                        typeof(TDbContext).Name);
                    return;
                }

                _logger.LogWarning("Retrying migration for {DbContext} in {Delay}...",
                    typeof(TDbContext).Name, delay);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, maxDelay.Ticks));
            }
        }
    }
}
