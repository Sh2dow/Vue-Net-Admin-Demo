using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Payments.Dtos;
using backend.Payments.Requests.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Payments.Handlers.Payments;

public sealed class GetPaymentByIdHandler : IRequestHandler<GetPaymentByIdQuery, PaymentViewDto?>
{
    private readonly PaymentsDbContext _db;

    public GetPaymentByIdHandler(PaymentsDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentViewDto?> Handle(GetPaymentByIdQuery req, CancellationToken ct)
    {
        var paymentRecord = await _db.PaymentEventRecords
            .AsNoTracking()
            .Where(x => x.PaymentId == req.Id)
            .OrderBy(x => x.SequenceNumber)
            .FirstOrDefaultAsync(ct);

        return paymentRecord == null ? null : new PaymentViewDto(
            paymentRecord.PaymentId,
            paymentRecord.OrderId,
            0, // Amount not stored in PaymentEventRecord - would need domain model extension
            paymentRecord.EventType,
            paymentRecord.OccurredAtUtc,
            null,
            null,
            null
        );
    }
}
