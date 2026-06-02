using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Messaging.Messages;
using backend.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace backend.Orders.Infrastructure.Orders;

public sealed class OrderExecutionDispatchConsumer : BackgroundService
{
    private const string ConsumerName = "order-execution-dispatch-consumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly OrderExecutionOptions _executionOptions;
    private readonly ILogger<OrderExecutionDispatchConsumer> _logger;

    public OrderExecutionDispatchConsumer(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> rabbitOptions,
        IOptions<OrderExecutionOptions> executionOptions,
        ILogger<OrderExecutionDispatchConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _rabbitOptions = rabbitOptions.Value;
        _executionOptions = executionOptions.Value;
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

                await RabbitMqTopology.EnsureConfiguredAsync(channel, _rabbitOptions, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, args) =>
                {
                    await HandleAsync(channel, args, stoppingToken);
                };

                await channel.BasicConsumeAsync(
                    queue: _rabbitOptions.OrderExecutionQueue,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Order execution dispatch consumer failed. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(_rabbitOptions.RetryDelaySeconds), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        var messageId = args.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IIntegrationEventOutbox>();

            var alreadyProcessed = await db.ConsumedMessages
                .AnyAsync(x => x.Consumer == ConsumerName && x.MessageId == messageId, ct);

            if (alreadyProcessed)
            {
                await channel.BasicAckAsync(args.DeliveryTag, false, ct);
                return;
            }

            var payload = Encoding.UTF8.GetString(args.Body.ToArray());
            var dispatched = IntegrationEventSerializer.Deserialize<OrderExecutionDispatchedMessage>(payload);

            _logger.LogInformation(
                "Execution dispatch emitted for order {OrderId} and payment {PaymentId}.",
                dispatched.OrderId,
                dispatched.PaymentId);

            var started = new OrderExecutionStartedMessage(
                dispatched.OrderId,
                dispatched.PaymentId,
                DateTime.UtcNow);

            await outbox.EnqueueAsync(
                IntegrationRoutingKeys.OrderExecutionStarted,
                started,
                dispatched.OrderId.ToString(),
                ct);

            if (_executionOptions.StubDelayMilliseconds > 0)
            {
                await Task.Delay(_executionOptions.StubDelayMilliseconds, ct);
            }

            if (_executionOptions.AutoComplete)
            {
                var completed = new OrderExecutionCompletedMessage(
                    dispatched.OrderId,
                    dispatched.PaymentId,
                    DateTime.UtcNow);

                await outbox.EnqueueAsync(
                    IntegrationRoutingKeys.OrderExecutionCompleted,
                    completed,
                    dispatched.OrderId.ToString(),
                    ct);
            }
            else
            {
                var failed = new OrderExecutionFailedMessage(
                    dispatched.OrderId,
                    dispatched.PaymentId,
                    "Stub execution failure.",
                    DateTime.UtcNow);

                await outbox.EnqueueAsync(
                    IntegrationRoutingKeys.OrderExecutionFailed,
                    failed,
                    dispatched.OrderId.ToString(),
                    ct);
            }

            db.ConsumedMessages.Add(new ConsumedMessage
            {
                Consumer = ConsumerName,
                MessageId = messageId
            });

            await db.SaveChangesAsync(ct);
            await channel.BasicAckAsync(args.DeliveryTag, false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle order execution dispatch message {MessageId}.", messageId);
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: true, cancellationToken: ct);
        }
    }
}
