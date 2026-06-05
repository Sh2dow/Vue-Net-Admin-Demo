using System.Text;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Infrastructure.Infrastructure.Messaging;

public sealed class OutboxDispatcher<TContext> : BackgroundService 
    where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutboxPublisher _publisher;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxDispatcher<TContext>> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOutboxPublisher publisher,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxDispatcher<TContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _publisher.EnsureInitializedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishBatchAsync(stoppingToken);
                if (!published)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox dispatch failed. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), stoppingToken);
            }
        }
    }

    private async Task<bool> PublishBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var messages = await db.Set<OutboxMessage>()
            .Where(x => x.PublishedAtUtc == null)
            .OrderBy(x => x.OccurredAtUtc)
            .Take(_options.OutboxBatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            _logger.LogDebug("No unpublished messages in outbox");
            return false;
        }

        foreach (var message in messages)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.Payload);
                await _publisher.PublishAsync(
                    message.RoutingKey,
                    body,
                    message.EventType,
                    message.CorrelationId,
                    message.Id.ToString(),
                    ct);

                message.PublishedAtUtc = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.PublishAttempts += 1;
                message.LastError = ex.Message;
                await db.SaveChangesAsync(ct);
                throw;
            }
        }

        await db.SaveChangesAsync(ct);
        return true;
    }
}
