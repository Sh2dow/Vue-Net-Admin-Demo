namespace backend.Payments.Dtos;

public sealed record PaymentViewDto(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? Provider,
    string? ProviderTransactionId
);

public sealed record PaymentDto(
    Guid Id,
    Guid UserId,
    decimal Amount,
    string Status,
    DateTime CreatedAtUtc
);

public sealed record CreatePaymentRequest(
    Guid UserId,
    decimal Amount,
    string Status
);

public sealed record UpdatePaymentRequest(
    decimal Amount,
    string Status
);
