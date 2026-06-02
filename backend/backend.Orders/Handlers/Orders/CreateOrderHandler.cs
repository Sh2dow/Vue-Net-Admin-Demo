using System.Threading;
using System.Threading.Tasks;
using backend.Orders.Dtos;
using backend.Orders.Requests.Orders;
using backend.Shared.Application.Results;
using backend.Shared.Application.Users;
using MediatR;

namespace backend.Orders.Handlers.Orders;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<OrderViewDto>>
{
    private readonly IMediator _mediator;

    public CreateOrderHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Result<OrderViewDto>> Handle(CreateOrderCommand req, CancellationToken ct)
    {
        var orderType = req.OrderType.Trim().ToLowerInvariant();

        return orderType switch
        {
            "digital" => await _mediator.Send(
                new CreateDigitalOrderCommand(req.TotalAmount, req.DownloadUrl ?? string.Empty),
                ct
            ),
            "physical" => await _mediator.Send(
                new CreatePhysicalOrderCommand(req.TotalAmount, req.ShippingAddress ?? string.Empty, req.TrackingNumber),
                ct
            ),
            _ => Result<OrderViewDto>.Validation([new ResultError("validation", "OrderType must be either 'digital' or 'physical'.", nameof(req.OrderType))])
        };
    }
}
