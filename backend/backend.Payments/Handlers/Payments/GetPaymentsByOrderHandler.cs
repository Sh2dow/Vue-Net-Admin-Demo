using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Payments.Dtos;
using backend.Payments.Requests.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Payments.Handlers.Payments;

public sealed class GetPaymentsByOrderHandler : IRequestHandler<GetPaymentsByOrderQuery, IReadOnlyList<PaymentViewDto>>
{
    private readonly PaymentsDbContext _db;

    public GetPaymentsByOrderHandler(PaymentsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PaymentViewDto>> Handle(GetPaymentsByOrderQuery req, CancellationToken ct)
    {
        var payments = await _db.PaymentEventRecords
            .AsNoTracking()
            .Where(x => x.OrderId == req.OrderId)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => new PaymentViewDto(
                x.PaymentId,
                x.OrderId,
                0, // Amount not stored in PaymentEventRecord - would need domain model extension
                x.EventType,
                x.OccurredAtUtc,
                null,
                null,
                null
            ))
            .ToListAsync(ct);

        return payments;
    }
}
