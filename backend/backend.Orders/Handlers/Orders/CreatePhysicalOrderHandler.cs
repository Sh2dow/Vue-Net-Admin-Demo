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

namespace backend.Orders.Handlers.Orders;

public sealed class CreatePhysicalOrderHandler : IRequestHandler<CreatePhysicalOrderCommand, Shared.Application.Results.Result<OrderViewDto>>
{
    private readonly OrdersDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly CreatePhysicalOrderCommandValidator _validator;

    public CreatePhysicalOrderHandler(
        OrdersDbContext db,
        IEffectiveUserAccessor effectiveUser,
        IIntegrationEventOutbox outbox,
        CreatePhysicalOrderCommandValidator validator)
    {
        _db = db;
        _effectiveUser = effectiveUser;
        _outbox = outbox;
        _validator = validator;
    }

    public async Task<Shared.Application.Results.Result<OrderViewDto>> Handle(CreatePhysicalOrderCommand req, CancellationToken ct)
    {
        // Command validation
        var commandResult = _validator.ValidateCommand(req);
        if (!commandResult.IsSuccess)
        {
            return Shared.Application.Results.Result<OrderViewDto>.ValidationFromDomainErrors(commandResult.Errors);
        }

        var userId = await _effectiveUser.GetUserIdAsync(ct);
        
        var order = new PhysicalOrder
        {
            UserId = userId,
            TotalAmount = req.TotalAmount,
            ShippingAddress = req.ShippingAddress.Trim(),
            TrackingNumber = string.IsNullOrWhiteSpace(req.TrackingNumber) ? null : req.TrackingNumber.Trim(),
            Status = OrderStatuses.PaymentPending
        };

        // Domain-level validation
        var domainResult = order.ValidatePhysicalOrder();
        if (!domainResult.IsSuccess)
        {
            return Shared.Application.Results.Result<OrderViewDto>.ValidationFromDomainErrors(domainResult.Errors);
        }

        _db.Orders.Add(order);

        await _outbox.EnqueueAsync(
            IntegrationRoutingKeys.OrderPaymentRequested,
            new OrderPaymentRequestedMessage(
                order.Id,
                userId,
                "physical",
                order.TotalAmount,
                DateTime.UtcNow),
            order.Id.ToString(),
            ct);

        await _db.SaveChangesAsync(ct);

        return Shared.Application.Results.Result<OrderViewDto>.Success(OrderMapper.ToDto(order));
    }
}
