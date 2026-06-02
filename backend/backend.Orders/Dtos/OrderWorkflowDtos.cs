using System;
using System.Collections.Generic;

namespace backend.Orders.Dtos;

public sealed record OrderPaymentEventDto(
    int AttemptNumber,
    int SequenceNumber,
    string EventType,
    DateTime OccurredAtUtc,
    string Description,
    string? Reason
);

public sealed record OrderPaymentDetailsDto(
    Guid OrderId,
    Guid? PaymentId,
    int CurrentAttemptNumber,
    string OrderStatus,
    string SagaState,
    string PaymentState,
    DateTime CreatedAtUtc,
    DateTime? LastPaymentRequestedAtUtc,
    DateTime? LastPaymentCompletedAtUtc,
    DateTime? ExecutionDispatchedAtUtc,
    DateTime? ExecutionStartedAtUtc,
    DateTime? ExecutionCompletedAtUtc,
    DateTime? ExecutionFailedAtUtc,
    string? ExecutionFailureReason,
    string? FailureReason,
    IReadOnlyList<OrderPaymentEventDto> Events
);

public sealed record OrderTimelineItemDto(
    string Key,
    string Label,
    string State,
    DateTime? OccurredAtUtc,
    string Description
);

public sealed record OrderWorkflowDto(
    OrderViewDto Order,
    OrderPaymentDetailsDto Payment,
    IReadOnlyList<OrderTimelineItemDto> Timeline
);
