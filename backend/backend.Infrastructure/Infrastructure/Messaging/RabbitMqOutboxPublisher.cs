using System.Text;
using backend.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace backend.Infrastructure.Infrastructure.Messaging;

public sealed class RabbitMqOutboxPublisher : IOutboxPublisher
{
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqOutboxPublisher> _logger;

    public RabbitMqOutboxPublisher(
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqOutboxPublisher> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqTopology.EnsureConfiguredAsync(channel, _options, ct);
    }

    public async Task PublishAsync(
        string routingKey,
        ReadOnlyMemory<byte> body,
        string? eventType,
        string? correlationId,
        string? messageId,
        CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = eventType ?? "unknown",
            MessageId = messageId,
            CorrelationId = correlationId
        };

        await channel.BasicPublishAsync(
            exchange: _options.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }
}
