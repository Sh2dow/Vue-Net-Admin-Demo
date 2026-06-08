using System.Text;
using Azure.Messaging.ServiceBus;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Orders.Application.Orders;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Messaging.Messages;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Functions.Functions;

public class OrderSagaFunction
{
    private const string ConsumerName = "order-saga-function";

    private readonly OrdersDbContext _db;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<OrderSagaFunction> _logger;

    public OrderSagaFunction(
        OrdersDbContext db,
        ServiceBusClient serviceBusClient,
        ILogger<OrderSagaFunction> logger)
    {
        _db = db;
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    [Function("OrderSagaProcessor")]
    public async Task Run(
        [ServiceBusTrigger("orders-saga", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        CancellationToken ct)
    {
        var messageId = message.MessageId ?? Guid.NewGuid().ToString("N");
        var eventType = message.Subject ?? "unknown";

        _logger.LogInformation(
            "{Consumer} received message: {MessageId}, Type: {EventType}",
            ConsumerName, messageId, eventType);

        var alreadyProcessed = await _db.ConsumedMessages
            .AnyAsync(x => x.Consumer == ConsumerName && x.MessageId == messageId, ct);

        if (alreadyProcessed)
        {
            _logger.LogInformation("Message already processed: {MessageId}", messageId);
            return;
        }

        var payload = Encoding.UTF8.GetString(message.Body.ToArray());

        try
        {
            switch (eventType)
            {
                case nameof(OrderPaymentRequestedMessage):
                {
                    var msg = IntegrationEventSerializer.Deserialize<OrderPaymentRequestedMessage>(payload);
                    await HandlePaymentRequestedAsync(msg, ct);
                    break;
                }
                case nameof(PaymentAuthorizedMessage):
                {
                    var msg = IntegrationEventSerializer.Deserialize<PaymentAuthorizedMessage>(payload);
                    await HandlePaymentAuthorizedAsync(msg, ct);
                    break;
                }
                case nameof(PaymentFailedMessage):
                {
                    var msg = IntegrationEventSerializer.Deserialize<PaymentFailedMessage>(payload);
                    await HandlePaymentFailedAsync(msg, ct);
                    break;
                }
                case nameof(OrderExecutionDispatchedMessage):
                {
                    var msg = IntegrationEventSerializer.Deserialize<OrderExecutionDispatchedMessage>(payload);
                    await HandleExecutionDispatchedAsync(msg, ct);
                    break;
                }
                case nameof(OrderExecutionStartedMessage):
                {
                    var msg = IntegrationEventSerializer.Deserialize<OrderExecutionStartedMessage>(payload);
                    await HandleExecutionStartedAsync(msg, ct);
                    break;
                }
                case nameof(OrderExecutionCompletedMessage):
                {
                    var msg = IntegrationEventSerializer.Deserialize<OrderExecutionCompletedMessage>(payload);
                    await HandleExecutionCompletedAsync(msg, ct);
                    break;
                }
                case nameof(OrderExecutionFailedMessage):
                {
                    var msg = IntegrationEventSerializer.Deserialize<OrderExecutionFailedMessage>(payload);
                    await HandleExecutionFailedAsync(msg, ct);
                    break;
                }
                default:
                {
                    _logger.LogWarning("Unknown event type: {EventType}", eventType);
                    break;
                }
            }

            _db.ConsumedMessages.Add(new ConsumedMessage
            {
                Consumer = ConsumerName,
                MessageId = messageId
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Consumer} failed to handle message {MessageId}.",
                ConsumerName, messageId);
            throw;
        }
    }

    private async Task HandlePaymentRequestedAsync(OrderPaymentRequestedMessage requested, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == requested.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {requested.OrderId} was not found for saga initialization.");

        var saga = await _db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == requested.OrderId, ct);
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
            _db.OrderSagaStates.Add(saga);
        }
        else if (!string.Equals(saga.State, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase))
        {
            saga.State = OrderSagaStates.PaymentPending;
            saga.LastPaymentRequestedAtUtc = requested.RequestedAtUtc;
            saga.UpdatedAtUtc = DateTime.UtcNow;
            saga.Version += 1;
        }

        order.Status = OrderStatuses.PaymentPending;
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandlePaymentAuthorizedAsync(PaymentAuthorizedMessage authorized, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == authorized.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {authorized.OrderId} was not found for payment authorization.");

        var saga = await _db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == authorized.OrderId, ct)
            ?? CreateMissingSaga(authorized.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase))
            return;

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
        await _db.SaveChangesAsync(ct);

        // Publish execution dispatch message directly
        await PublishMessageAsync(
            "orders-execution-dispatch",
            new OrderExecutionDispatchedMessage(order.Id, authorized.PaymentId, DateTime.UtcNow),
            order.Id.ToString(),
            ct);

        await PublishMessageAsync(
            "orders-saga",
            new OrderExecutionDispatchedMessage(order.Id, authorized.PaymentId, DateTime.UtcNow),
            order.Id.ToString(),
            ct);
    }

    private async Task HandlePaymentFailedAsync(PaymentFailedMessage failed, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == failed.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {failed.OrderId} was not found for payment failure.");

        var saga = await _db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == failed.OrderId, ct)
            ?? CreateMissingSaga(failed.OrderId);

        if (string.Equals(saga.State, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase))
            return;

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
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleExecutionDispatchedAsync(OrderExecutionDispatchedMessage dispatched, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == dispatched.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {dispatched.OrderId} was not found for execution dispatch.");

        var saga = await _db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == dispatched.OrderId, ct)
            ?? CreateMissingSaga(dispatched.OrderId);

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
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleExecutionStartedAsync(OrderExecutionStartedMessage started, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == started.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {started.OrderId} was not found for execution start.");

        var saga = await _db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == started.OrderId, ct)
            ?? CreateMissingSaga(started.OrderId);

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
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleExecutionCompletedAsync(OrderExecutionCompletedMessage completed, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == completed.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {completed.OrderId} was not found for execution completion.");

        var saga = await _db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == completed.OrderId, ct)
            ?? CreateMissingSaga(completed.OrderId);

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
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleExecutionFailedAsync(OrderExecutionFailedMessage failed, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == failed.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {failed.OrderId} was not found for execution failure.");

        var saga = await _db.OrderSagaStates.FirstOrDefaultAsync(x => x.OrderId == failed.OrderId, ct)
            ?? CreateMissingSaga(failed.OrderId);

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
        await _db.SaveChangesAsync(ct);
    }

    private OrderSagaState CreateMissingSaga(Guid orderId)
    {
        var saga = new OrderSagaState
        {
            OrderId = orderId,
            State = OrderSagaStates.PaymentPending,
            UpdatedAtUtc = DateTime.UtcNow,
            Version = 0
        };
        _db.OrderSagaStates.Add(saga);
        return saga;
    }

    private async Task PublishMessageAsync<T>(string queueName, T message, string correlationId, CancellationToken ct)
    {
        var sender = _serviceBusClient.CreateSender(queueName);
        await using (sender.ConfigureAwait(false))
        {
            var body = Encoding.UTF8.GetBytes(IntegrationEventSerializer.Serialize(message));
            var sbMessage = new ServiceBusMessage(body)
            {
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId,
                ContentType = "application/json",
                Subject = typeof(T).Name
            };
            await sender.SendMessageAsync(sbMessage, ct);
        }
    }
}
