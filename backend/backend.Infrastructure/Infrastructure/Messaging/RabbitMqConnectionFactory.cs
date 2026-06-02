using backend.Shared.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace backend.Infrastructure.Infrastructure.Messaging;

public sealed class RabbitMqConnectionFactory
{
    private readonly RabbitMqOptions _options;

    public RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public Task<IConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.Uri)
        };

        return factory.CreateConnectionAsync(ct);
    }
}
