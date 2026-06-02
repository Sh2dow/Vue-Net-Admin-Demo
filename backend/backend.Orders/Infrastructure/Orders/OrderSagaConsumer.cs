using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Orders.Application.Orders;
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

public sealed class OrderSagaConsumer : BackgroundService
{
    private const string ConsumerName = "order-saga-consumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly ILogger<OrderSagaConsumer> _logger;

    public OrderSagaConsumer(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> rabbitOptions,
        ILogger<OrderSagaConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _rabbitOptions = rabbitOptions.Value;
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
                    queue: _rabbitOptions.OrderSagaQueue,
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
                _logger.LogWarning(ex, "Order saga consumer failed. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(_rabbitOptions.RetryDelaySeconds), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        var messageId = args.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N");
        var eventType = args.BasicProperties.Type ?? string.Empty;

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

            switch (eventType)
            {
                case nameof(OrderPaymentRequestedMessage):
                {
                    var requested = IntegrationEventSerializer.Deserialize<OrderPaymentRequestedMessage>(payload);
                    await HandlePaymentRequestedAsync(db, requested, ct);
                    break;
                }
                case nameof(PaymentAuthorizedMessage):
                {
                    var authorized = IntegrationEventSerializer.Deserialize<PaymentAuthorizedMessage>(payload);
                    await HandlePaymentAuthorizedAsync(db, outbox, authorized, ct);
                    break;
                }
                case nameof(PaymentFailedMessage):
                {
                    var failed = IntegrationEventSerializer.Deserialize<PaymentFailedMessage>(payload);
                    await HandlePaymentFailedAsync(db, failed, ct);
                    break;
                }
                case nameof(OrderExecutionDispatchedMessage):
                {
                    var dispatched = IntegrationEventSerializer.Deserialize<OrderExecutionDispatchedMessage>(payload);
                    await HandleExecutionDispatchedAsync(db, dispatched, ct);
                    break;
                }
                case nameof(OrderExecutionStartedMessage):
                {
                    var started = IntegrationEventSerializer.Deserialize<OrderExecutionStartedMessage>(payload);
                    await HandleExecutionStartedAsync(db, started, ct);
                    break;
                }
                case nameof(OrderExecutionCompletedMessage):
                {
                    var completed = IntegrationEventSerializer.Deserialize<OrderExecutionCompletedMessage>(payload);
                    await HandleExecutionCompletedAsync(db, completed, ct);
                    break;
                }
                case nameof(OrderExecutionFailedMessage):
                {
                    var failedExecution = IntegrationEventSerializer.Deserialize<OrderExecutionFailedMessage>(payload);
                    await HandleExecutionFailedAsync(db, failedExecution, ct);
                    break;
                }
                default:
                    await channel.BasicAckAsync(args.DeliveryTag, false, ct);
                    return;
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
            _logger.LogWarning(ex, "Failed to handle saga message {MessageId}.", messageId);
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: true, cancellationToken: ct);
        }
    }

    private static async Task HandlePaymentRequestedAsync(
        OrdersDbContext db,
        OrderPaymentRequestedMessage requested,
        CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == requested.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {requested.OrderId} was not found for saga initialization.");

        var saga = await db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == requested.OrderId, ct);
        if (saga == null)
        {
            saga = new OrderSagaState
            {
                OrderId = requested.OrderId,
                State = OrderSagaStates.PaymentPending,
                LastPaymentRequestedAtUtc = requested.RequestedAtUtc,
                UpdatedAtUtc = DateTime.UtcNow,
                Version = 1
            };

            db.OrderSagaStates.Add(saga);
        }
        else if (!string.Equals(saga.State, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase))
        {
            saga.State = OrderSagaStates.PaymentPending;
            saga.LastPaymentRequestedAtUtc = requested.RequestedAtUtc;
            saga.UpdatedAtUtc = DateTime.UtcNow;
            saga.Version += 1;
        }

        order.Status = OrderStatuses.PaymentPending;
    }

    private static async Task HandlePaymentAuthorizedAsync(
        OrdersDbContext db,
        IIntegrationEventOutbox outbox,
        PaymentAuthorizedMessage authorized,
        CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == authorized.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {authorized.OrderId} was not found for payment authorization.");

        var saga = await db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == authorized.OrderId, ct)
            ?? CreateMissingSaga(db, authorized.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(saga.State, OrderSagaStates.PaymentAuthorized, StringComparison.OrdinalIgnoreCase)
            && saga.PaymentId == authorized.PaymentId)
        {
            return;
        }

        saga.PaymentId = authorized.PaymentId;
        saga.State = OrderSagaStates.PaymentAuthorized;
        saga.LastPaymentCompletedAtUtc = authorized.OccurredAtUtc;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        saga.Version += 1;

        order.Status = OrderStatuses.PaymentAuthorized;

        await outbox.EnqueueAsync(
            IntegrationRoutingKeys.OrderExecutionDispatched,
            new OrderExecutionDispatchedMessage(order.Id, authorized.PaymentId, DateTime.UtcNow),
            order.Id.ToString(),
            ct);
    }

    private static async Task HandlePaymentFailedAsync(
        OrdersDbContext db,
        PaymentFailedMessage failed,
        CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == failed.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {failed.OrderId} was not found for payment failure.");

        var saga = await db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == failed.OrderId, ct)
            ?? CreateMissingSaga(db, failed.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(saga.State, OrderSagaStates.PaymentFailed, StringComparison.OrdinalIgnoreCase)
            && saga.PaymentId == failed.PaymentId)
        {
            return;
        }

        saga.PaymentId = failed.PaymentId;
        saga.State = OrderSagaStates.PaymentFailed;
        saga.LastPaymentCompletedAtUtc = failed.OccurredAtUtc;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        saga.Version += 1;

        order.Status = OrderStatuses.PaymentFailed;
    }

    private static async Task HandleExecutionDispatchedAsync(
        OrdersDbContext db,
        OrderExecutionDispatchedMessage dispatched,
        CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == dispatched.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {dispatched.OrderId} was not found for execution dispatch.");

        var saga = await db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == dispatched.OrderId, ct)
            ?? CreateMissingSaga(db, dispatched.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase)
            && saga.PaymentId == dispatched.PaymentId)
        {
            return;
        }

        saga.PaymentId ??= dispatched.PaymentId;
        saga.State = OrderSagaStates.ExecutionDispatched;
        saga.ExecutionDispatchedAtUtc = dispatched.DispatchedAtUtc;
        saga.ExecutionStartedAtUtc = null;
        saga.ExecutionCompletedAtUtc = null;
        saga.ExecutionFailedAtUtc = null;
        saga.ExecutionFailureReason = null;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        saga.Version += 1;

        order.Status = OrderStatuses.ExecutionDispatched;
    }

    private static async Task HandleExecutionStartedAsync(
        OrdersDbContext db,
        OrderExecutionStartedMessage started,
        CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == started.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {started.OrderId} was not found for execution start.");

        var saga = await db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == started.OrderId, ct)
            ?? CreateMissingSaga(db, started.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionStarted, StringComparison.OrdinalIgnoreCase)
            && saga.PaymentId == started.PaymentId)
        {
            return;
        }

        saga.PaymentId ??= started.PaymentId;
        saga.State = OrderSagaStates.ExecutionStarted;
        saga.ExecutionStartedAtUtc = started.StartedAtUtc;
        saga.ExecutionCompletedAtUtc = null;
        saga.ExecutionFailedAtUtc = null;
        saga.ExecutionFailureReason = null;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        saga.Version += 1;

        order.Status = OrderStatuses.ExecutionStarted;
    }

    private static async Task HandleExecutionCompletedAsync(
        OrdersDbContext db,
        OrderExecutionCompletedMessage completed,
        CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == completed.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {completed.OrderId} was not found for execution completion.");

        var saga = await db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == completed.OrderId, ct)
            ?? CreateMissingSaga(db, completed.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionCompleted, StringComparison.OrdinalIgnoreCase)
            && saga.PaymentId == completed.PaymentId)
        {
            return;
        }

        saga.PaymentId ??= completed.PaymentId;
        saga.State = OrderSagaStates.ExecutionCompleted;
        saga.ExecutionCompletedAtUtc = completed.CompletedAtUtc;
        saga.ExecutionFailedAtUtc = null;
        saga.ExecutionFailureReason = null;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        saga.Version += 1;

        order.Status = OrderStatuses.ExecutionCompleted;
    }

    private static async Task HandleExecutionFailedAsync(
        OrdersDbContext db,
        OrderExecutionFailedMessage failed,
        CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == failed.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {failed.OrderId} was not found for execution failure.");

        var saga = await db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == failed.OrderId, ct)
            ?? CreateMissingSaga(db, failed.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionFailed, StringComparison.OrdinalIgnoreCase)
            && saga.PaymentId == failed.PaymentId
            && string.Equals(saga.ExecutionFailureReason, failed.Reason, StringComparison.Ordinal))
        {
            return;
        }

        saga.PaymentId ??= failed.PaymentId;
        saga.State = OrderSagaStates.ExecutionFailed;
        saga.ExecutionFailedAtUtc = failed.FailedAtUtc;
        saga.ExecutionFailureReason = failed.Reason;
        saga.ExecutionCompletedAtUtc = null;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        saga.Version += 1;

        order.Status = OrderStatuses.ExecutionFailed;
    }

    private static OrderSagaState CreateMissingSaga(OrdersDbContext db, Guid orderId)
    {
        var saga = new OrderSagaState
        {
            OrderId = orderId,
            State = OrderSagaStates.PaymentPending,
            UpdatedAtUtc = DateTime.UtcNow,
            Version = 0
        };

        db.OrderSagaStates.Add(saga);
        return saga;
    }
}
