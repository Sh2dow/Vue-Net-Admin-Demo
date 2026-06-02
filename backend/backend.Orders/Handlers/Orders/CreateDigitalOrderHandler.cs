using System;
using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Orders.Application.Orders;
using backend.Orders.Dtos;
using backend.Orders.Mappers;
using backend.Orders.Requests.Orders;
using backend.Orders.Validation.Orders;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Messaging.Messages;
using backend.Shared.Application.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace backend.Orders.Handlers.Orders;

public sealed class CreateDigitalOrderHandler : IRequestHandler<CreateDigitalOrderCommand, Shared.Application.Results.Result<OrderViewDto>>
{
    private readonly OrdersDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly CreateDigitalOrderCommandValidator _validator;
    private readonly ILogger<CreateDigitalOrderHandler> _logger;

    public CreateDigitalOrderHandler(
        OrdersDbContext db,
        IEffectiveUserAccessor effectiveUser,
        IIntegrationEventOutbox outbox,
        CreateDigitalOrderCommandValidator validator,
        ILogger<CreateDigitalOrderHandler> logger)
    {
        _db = db;
        _effectiveUser = effectiveUser;
        _outbox = outbox;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Shared.Application.Results.Result<OrderViewDto>> Handle(CreateDigitalOrderCommand req, CancellationToken ct)
    {
        var userId = await _effectiveUser.GetUserIdAsync(ct);
        
        var order = new DigitalOrder
        {
            UserId = userId,
            TotalAmount = req.TotalAmount,
            DownloadUrl = req.DownloadUrl.Trim(),
            Status = OrderStatuses.PaymentPending
        };

        _db.Orders.Add(order);

        await _outbox.EnqueueAsync(
            IntegrationRoutingKeys.OrderPaymentRequested,
            new OrderPaymentRequestedMessage(
                order.Id,
                userId,
                "digital",
                order.TotalAmount,
                DateTime.UtcNow),
            order.Id.ToString(),
            ct);


        await _db.SaveChangesAsync(ct);

        return Shared.Application.Results.Result<OrderViewDto>.Success(OrderMapper.ToDto(order));
    }
}
