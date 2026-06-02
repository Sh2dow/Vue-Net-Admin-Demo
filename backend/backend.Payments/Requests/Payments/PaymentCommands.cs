using System;
using backend.Payments.Dtos;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;

namespace backend.Payments.Requests.Payments;

public sealed record CreatePaymentCommand(
    decimal Amount,
    string Provider,
    string? ProviderTransactionId
) : ICommand<Result<PaymentViewDto>>;

public sealed record RetryPaymentCommand(Guid PaymentId) : ICommand<Result<PaymentViewDto>>;
