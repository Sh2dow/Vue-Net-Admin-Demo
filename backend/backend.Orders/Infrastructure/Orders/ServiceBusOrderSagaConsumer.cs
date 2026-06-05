using Azure.Messaging.ServiceBus;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Orders.Application.Orders;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Messaging.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace backend.Orders.Infrastructure.Orders;

public sealed class ServiceBusOrderSagaConsumer : ServiceBusConsumerBase
{
    private const string Consumer = "order-saga-consumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceBusOrderSagaConsumer> _logger;

    public ServiceBusOrderSagaConsumer(
        ServiceBusClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusOrderSagaConsumer> logger)
        : base(client, logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override string QueueName => "orders-saga";
    protected override string ConsumerName => Consumer;
    protected override IServiceScopeFactory ScopeFactory => _scopeFactory;

    protected override async Task HandleCoreAsync(
        string payload,
        string eventType,
        string messageId,
        IServiceScope scope,
        OrdersDbContext db,
        CancellationToken ct)
    {
        var outbox = scope.ServiceProvider.GetRequiredService<IIntegrationEventOutbox>();

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
