using System.Text;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace backend.Infrastructure.Infrastructure.Messaging;

public sealed class OutboxDispatcher<TContext> : BackgroundService 
    where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxDispatcher<TContext>> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxDispatcher<TContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
                
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await RabbitMqTopology.EnsureConfiguredAsync(channel, _options, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var published = await PublishBatchAsync(channel, stoppingToken);
                    if (!published)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), stoppingToken);
            }
        }
    }

    private async Task<bool> PublishBatchAsync(IChannel channel, CancellationToken ct)
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
                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Type = message.EventType,
                    MessageId = message.Id.ToString(),
                    CorrelationId = message.CorrelationId
                };

                var body = Encoding.UTF8.GetBytes(message.Payload);
                await channel.BasicPublishAsync(
                    exchange: _options.Exchange,
                    routingKey: message.RoutingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: ct);

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
