using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Orders.Application.Orders;
using backend.Orders.Dtos;
using backend.Orders.Mappers;
using backend.Orders.Requests.Orders;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Messaging.Messages;
using backend.Shared.Application.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Orders.Handlers.Orders;

public sealed class GetOrderWorkflowHandler : IRequestHandler<GetOrderWorkflowQuery, OrderWorkflowDto?>
{
    private readonly OrdersDbContext _ordersDb;
    private readonly PaymentsDbContext _paymentsDb;
    private readonly IEffectiveUserAccessor _effectiveUser;

    public GetOrderWorkflowHandler(OrdersDbContext ordersDb, PaymentsDbContext paymentsDb, IEffectiveUserAccessor effectiveUser)
    {
        _ordersDb = ordersDb;
        _paymentsDb = paymentsDb;
        _effectiveUser = effectiveUser;
    }

    public async Task<OrderWorkflowDto?> Handle(GetOrderWorkflowQuery req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var order = await _ordersDb.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.OrderId && x.UserId == userId, ct);

        if (order == null)
        {
            return null;
        }

        var saga = await _ordersDb.OrderSagaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == req.OrderId, ct);

        var paymentEvents = await _paymentsDb.PaymentEventRecords
            .AsNoTracking()
            .Where(x => x.OrderId == req.OrderId)
            .OrderBy(x => x.SequenceNumber)
            .ToListAsync(ct);

        var failureReason = paymentEvents
            .Where(x => string.Equals(x.EventType, nameof(PaymentFailedMessage), StringComparison.Ordinal))
            .Select(x => TryGetFailureReason(x.Data))
            .LastOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var currentAttemptNumber = paymentEvents
            .OrderByDescending(x => x.AttemptNumber)
            .Select(x => x.AttemptNumber)
            .FirstOrDefault();

        var payment = new OrderPaymentDetailsDto(
            order.Id,
            saga?.PaymentId ?? paymentEvents.LastOrDefault()?.PaymentId,
            currentAttemptNumber,
            order.Status,
            saga?.State ?? OrderSagaStates.PaymentPending,
            ResolvePaymentState(order.Status, saga?.State, paymentEvents),
            order.CreatedAtUtc,
            saga?.LastPaymentRequestedAtUtc,
            saga?.LastPaymentCompletedAtUtc,
            saga?.ExecutionDispatchedAtUtc,
            saga?.ExecutionStartedAtUtc,
            saga?.ExecutionCompletedAtUtc,
            saga?.ExecutionFailedAtUtc,
            saga?.ExecutionFailureReason,
            failureReason,
            paymentEvents.Select(MapPaymentEvent).ToArray()
        );

        var authorizedAtUtc = GetLatestEventTime(paymentEvents, nameof(PaymentAuthorizedMessage));
        var failedEvent = GetLatestEvent(paymentEvents, nameof(PaymentFailedMessage));
        var initiatedAtUtc = GetLatestEventTime(paymentEvents, nameof(PaymentInitiatedMessage));

        var timeline = new[]
        {
            new OrderTimelineItemDto(
                "order-created",
                "Order created",
                "Completed",
                order.CreatedAtUtc,
                "Order was created and stored."),
            new OrderTimelineItemDto(
                "payment-requested",
                "Payment requested",
                saga?.LastPaymentRequestedAtUtc != null ? "Completed" : "Pending",
                saga?.LastPaymentRequestedAtUtc,
                "Order entered the payment workflow."),
            new OrderTimelineItemDto(
                "payment-initiated",
                "Payment initiated",
                initiatedAtUtc != null ? "Completed" : "Pending",
                initiatedAtUtc,
                "Payment service accepted the work item."),
            new OrderTimelineItemDto(
                "payment-authorized",
                "Payment authorized",
                authorizedAtUtc != null ? "Completed" : "Pending",
                authorizedAtUtc,
                "Payment was authorized successfully."),
            new OrderTimelineItemDto(
                "payment-failed",
                "Payment failed",
                failedEvent != null ? "Completed" : "Pending",
                failedEvent?.OccurredAtUtc,
                failedEvent == null
                    ? "No payment failure has been recorded."
                    : $"Payment failed: {TryGetFailureReason(failedEvent.Data) ?? "Unknown reason."}"),
            new OrderTimelineItemDto(
                "execution-dispatched",
                "Execution dispatched",
                saga?.ExecutionDispatchedAtUtc != null ? "Completed" : "Pending",
                saga?.ExecutionDispatchedAtUtc,
                "Order execution was dispatched after payment success."),
            new OrderTimelineItemDto(
                "execution-started",
                "Execution started",
                saga?.ExecutionStartedAtUtc != null ? "Completed" : "Pending",
                saga?.ExecutionStartedAtUtc,
                "Execution workers started processing the order."),
            new OrderTimelineItemDto(
                "execution-completed",
                "Execution completed",
                saga?.ExecutionCompletedAtUtc != null ? "Completed" : "Pending",
                saga?.ExecutionCompletedAtUtc,
                "Order execution completed successfully."),
            new OrderTimelineItemDto(
                "execution-failed",
                "Execution failed",
                saga?.ExecutionFailedAtUtc != null ? "Completed" : "Pending",
                saga?.ExecutionFailedAtUtc,
                saga?.ExecutionFailedAtUtc == null
                    ? "No execution failure has been recorded."
                    : $"Execution failed: {saga.ExecutionFailureReason ?? "Unknown reason."}")
        };

        return new OrderWorkflowDto(
            OrderMapper.ToDto(order),
            payment,
            timeline
        );
    }

    private static OrderPaymentEventDto MapPaymentEvent(PaymentEventRecord record)
    {
        return record.EventType switch
        {
            nameof(PaymentInitiatedMessage) => new OrderPaymentEventDto(
                record.AttemptNumber,
                record.SequenceNumber,
                record.EventType,
                record.OccurredAtUtc,
                "Payment workflow started.",
                null),
            nameof(PaymentAuthorizedMessage) => new OrderPaymentEventDto(
                record.AttemptNumber,
                record.SequenceNumber,
                record.EventType,
                record.OccurredAtUtc,
                "Payment was authorized.",
                null),
            nameof(PaymentFailedMessage) => new OrderPaymentEventDto(
                record.AttemptNumber,
                record.SequenceNumber,
                record.EventType,
                record.OccurredAtUtc,
                "Payment failed.",
                TryGetFailureReason(record.Data)),
            _ => new OrderPaymentEventDto(
                record.AttemptNumber,
                record.SequenceNumber,
                record.EventType,
                record.OccurredAtUtc,
                "Payment event recorded.",
                null)
        };
    }

    private static string ResolvePaymentState(string orderStatus, string? sagaState, IReadOnlyCollection<PaymentEventRecord> paymentEvents)
    {
        if (string.Equals(orderStatus, OrderStatuses.PaymentFailed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sagaState, OrderSagaStates.PaymentFailed, StringComparison.OrdinalIgnoreCase))
        {
            return OrderSagaStates.PaymentFailed;
        }

        if (string.Equals(orderStatus, OrderStatuses.ExecutionDispatched, StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderStatus, OrderStatuses.ExecutionStarted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderStatus, OrderStatuses.ExecutionCompleted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderStatus, OrderStatuses.ExecutionFailed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderStatus, OrderStatuses.PaymentAuthorized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sagaState, OrderSagaStates.ExecutionDispatched, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sagaState, OrderSagaStates.ExecutionStarted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sagaState, OrderSagaStates.ExecutionCompleted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sagaState, OrderSagaStates.ExecutionFailed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sagaState, OrderSagaStates.PaymentAuthorized, StringComparison.OrdinalIgnoreCase)
            || paymentEvents.Any(x => string.Equals(x.EventType, nameof(PaymentAuthorizedMessage), StringComparison.Ordinal)))
        {
            return OrderSagaStates.PaymentAuthorized;
        }

        if (paymentEvents.Any(x => string.Equals(x.EventType, nameof(PaymentInitiatedMessage), StringComparison.Ordinal)))
        {
            return nameof(PaymentInitiatedMessage);
        }

        return OrderSagaStates.PaymentPending;
    }

    private static DateTime? GetLatestEventTime(IReadOnlyCollection<PaymentEventRecord> paymentEvents, string eventType)
    {
        return paymentEvents
            .Where(x => string.Equals(x.EventType, eventType, StringComparison.Ordinal))
            .OrderByDescending(x => x.SequenceNumber)
            .Select(x => (DateTime?)x.OccurredAtUtc)
            .FirstOrDefault();
    }

    private static PaymentEventRecord? GetLatestEvent(IReadOnlyCollection<PaymentEventRecord> paymentEvents, string eventType)
    {
        return paymentEvents
            .Where(x => string.Equals(x.EventType, eventType, StringComparison.Ordinal))
            .OrderByDescending(x => x.SequenceNumber)
            .FirstOrDefault();
    }

    private static string? TryGetFailureReason(string payload)
    {
        try
        {
            return IntegrationEventSerializer.Deserialize<PaymentFailedMessage>(payload).Reason;
        }
        catch
        {
            return null;
        }
    }
}
