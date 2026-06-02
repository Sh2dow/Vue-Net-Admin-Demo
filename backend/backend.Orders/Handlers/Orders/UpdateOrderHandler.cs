using System.Threading;
using System.Threading.Tasks;
using backend.Domain.Data;
using backend.Domain.Models;
using backend.Orders.Dtos;
using backend.Orders.Mappers;
using backend.Orders.Requests.Orders;
using backend.Orders.Validation.Orders;
using backend.Shared.Application.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ResultError = backend.Shared.Application.Results.ResultError;

namespace backend.Orders.Handlers.Orders;

public sealed class UpdateOrderHandler : IRequestHandler<UpdateOrderCommand, Shared.Application.Results.Result<OrderViewDto>>
{
    private readonly OrdersDbContext _db;
    private readonly IEffectiveUserAccessor _effectiveUser;
    private readonly UpdateOrderCommandValidator _validator;

    public UpdateOrderHandler(OrdersDbContext db, IEffectiveUserAccessor effectiveUser, UpdateOrderCommandValidator validator)
    {
        _db = db;
        _effectiveUser = effectiveUser;
        _validator = validator;
    }

    public async Task<Shared.Application.Results.Result<OrderViewDto>> Handle(UpdateOrderCommand req, CancellationToken ct)
    {
           // Command validation
        var commandResult = _validator.ValidateCommand(req);
        if (!commandResult.IsSuccess)
        {
            return Shared.Application.Results.Result<OrderViewDto>.ValidationFromDomainErrors(commandResult.Errors);
        }

        var userId = await _effectiveUser.GetUserIdAsync(ct);
        var order = await _db.Orders
            .FirstOrDefaultAsync(x => x.Id == req.Id && x.UserId == userId, ct);
        if (order == null) return Shared.Application.Results.Result<OrderViewDto>.NotFound("Order not found.");

        order.TotalAmount = req.TotalAmount;
        order.Status = req.Status.Trim();

        switch (order)
        {
            case DigitalOrder digital:
                if (string.IsNullOrWhiteSpace(req.DownloadUrl))
                {
                    return Shared.Application.Results.Result<OrderViewDto>.Validation([new ResultError("validation", "DownloadUrl is required for digital orders.", nameof(req.DownloadUrl))]);
                }

                digital.DownloadUrl = req.DownloadUrl.Trim();
                break;
            case PhysicalOrder physical:
                if (string.IsNullOrWhiteSpace(req.ShippingAddress))
                {
                    return Shared.Application.Results.Result<OrderViewDto>.Validation([new ResultError("validation", "ShippingAddress is required for physical orders.", nameof(req.ShippingAddress))]);
                }

                physical.ShippingAddress = req.ShippingAddress.Trim();
                physical.TrackingNumber = string.IsNullOrWhiteSpace(req.TrackingNumber) ? null : req.TrackingNumber.Trim();
                break;
        }

        // Domain-level validation
        DomainResult<DomainUnit> domainResult;
        switch (order)
        {
            case DigitalOrder digital:
                domainResult = order.ValidateDigitalOrder();
                break;
            case PhysicalOrder physical:
                domainResult = order.ValidatePhysicalOrder();
                break;
            default:
                domainResult = order.Validate();
                break;
        }
        
           if (!domainResult.IsSuccess)
        {
            return Shared.Application.Results.Result<OrderViewDto>.ValidationFromDomainErrors(domainResult.Errors);
        }

        await _db.SaveChangesAsync(ct);

        return Shared.Application.Results.Result<OrderViewDto>.Success(OrderMapper.ToDto(order));
    }
}


