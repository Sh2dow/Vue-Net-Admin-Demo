using backend.Shared.Configuration;
using RabbitMQ.Client;

namespace backend.Infrastructure.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public static async Task EnsureConfiguredAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            options.DeadLetterExchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await DeclareQueueAsync(
            channel,
            options.PaymentRequestQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "orders.payment-requested",
            ct);

        await DeclareQueueAsync(
            channel,
            options.OrderSagaQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "orders.payment-requested",
            ct);

        await DeclareQueueAsync(
            channel,
            options.OrderSagaQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "payments.*",
            ct);

        await DeclareQueueAsync(
            channel,
            options.OrderSagaQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "orders.execution-dispatched",
            ct);

        await DeclareQueueAsync(
            channel,
            options.OrderSagaQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "orders.execution-started",
            ct);

        await DeclareQueueAsync(
            channel,
            options.OrderSagaQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "orders.execution-completed",
            ct);

        await DeclareQueueAsync(
            channel,
            options.OrderSagaQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "orders.execution-failed",
            ct);

        await DeclareQueueAsync(
            channel,
            options.OrderExecutionQueue,
            options.Exchange,
            options.DeadLetterExchange,
            "orders.execution-dispatched",
            ct);
    }

    private static async Task DeclareQueueAsync(
        IChannel channel,
        string queueName,
        string exchange,
        string deadLetterExchange,
        string routingKey,
        CancellationToken ct)
    {
        var deadLetterQueue = $"{queueName}.dead";

        await channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange
            },
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queueName,
            exchange,
            routingKey,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            deadLetterQueue,
            deadLetterExchange,
            routingKey,
            arguments: null,
            cancellationToken: ct);
    }
}
