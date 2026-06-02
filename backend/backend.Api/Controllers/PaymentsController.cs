using System.Net.Http.Json;
using backend.Payments.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace backend.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public PaymentsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private string WithQueryString(string path) =>
        string.IsNullOrEmpty(Request.QueryString.Value)
            ? path
            : path + Request.QueryString.Value;

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, WithQueryString(path));

        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _httpClientFactory.CreateClient("Payments").SendAsync(request, ct);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetPayments(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/payments", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var payments = await response.Content.ReadFromJsonAsync<IReadOnlyList<PaymentDto>>(ct);
        return Ok(payments);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> GetPaymentById(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/payments/{id}", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>(ct);
        return payment == null ? NotFound() : Ok(payment);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentDto>> CreatePayment(CreatePaymentRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/payments", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>(ct);
        return payment == null ? NotFound() : CreatedAtAction(nameof(GetPaymentById), new { id = payment.Id }, payment);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> UpdatePayment(Guid id, UpdatePaymentRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Put, $"api/payments/{id}", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>(ct);
        return Ok(payment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePayment(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/payments/{id}", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        return NoContent();
    }
}
