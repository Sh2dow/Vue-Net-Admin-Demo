using backend.Domain.Data;
using backend.Payments.Dtos;
using backend.Payments.Requests.Payments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace backend.Payments.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ISender _sender;

    public PaymentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentViewDto>> GetPaymentById(Guid id, CancellationToken ct)
    {
        var payment = await _sender.Send(new GetPaymentByIdQuery(id), ct);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<ActionResult<IReadOnlyList<PaymentViewDto>>> GetPaymentsByOrder(Guid orderId, CancellationToken ct)
    {
        var payments = await _sender.Send(new GetPaymentsByOrderQuery(orderId), ct);
        return Ok(payments);
    }

    [HttpPost("order/{orderId:guid}/create")]
    public async Task<ActionResult<PaymentViewDto>> CreatePayment(Guid orderId, CreatePaymentCommand command, CancellationToken ct)
    {
        // CreatePaymentCommand needs to include OrderId - for now just log the orderId
        // In a real implementation, the command would be constructed with orderId in the handler
        return BadRequest("Payment creation with order reference not yet implemented");
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<PaymentViewDto>> RetryPayment(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new RetryPaymentCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
