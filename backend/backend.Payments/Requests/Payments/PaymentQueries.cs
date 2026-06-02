using System;
using backend.Payments.Dtos;
using backend.Shared.Application.Abstractions;

namespace backend.Payments.Requests.Payments;

public sealed record GetPaymentByIdQuery(Guid Id) : IQuery<PaymentViewDto?>;
public sealed record GetPaymentsByOrderQuery(Guid OrderId) : IQuery<IReadOnlyList<PaymentViewDto>>;
