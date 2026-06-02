using backend.Domain.Data;
using backend.Domain.Models;
using backend.Shared.Application.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Infrastructure.Infrastructure.Messaging;

public sealed class IntegrationEventOutbox<TContext> : IIntegrationEventOutbox 
    where TContext : DbContext
{
    private readonly TContext _db;
    private readonly ILogger<IntegrationEventOutbox<TContext>> _logger;

    public IntegrationEventOutbox(TContext db, ILogger<IntegrationEventOutbox<TContext>> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task EnqueueAsync<T>(
        string routingKey,
        T message,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        var outboxMessage = new OutboxMessage
        {
            EventType = typeof(T).Name,
            RoutingKey = routingKey,
            Payload = IntegrationEventSerializer.Serialize(message),
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        };

        _db.Set<OutboxMessage>().Add(outboxMessage);
        return Task.CompletedTask;
    }
}
