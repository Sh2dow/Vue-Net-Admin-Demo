using System.Net.Http.Json;
using backend.Orders.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace backend.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OrdersController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private string WithQueryString(string path) =>
        string.IsNullOrEmpty(Request.QueryString.Value)
            ? path
            : path + Request.QueryString.Value;

    private async Task<HttpResponseMessage> ForwardRequestAsync(HttpMethod method, string path, object? body, CancellationToken ct)
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
        
        return await _httpClientFactory.CreateClient("Orders").SendAsync(request, ct);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrders(CancellationToken ct)
    {
        using var response = await ForwardRequestAsync(HttpMethod.Get, "api/orders", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var orders = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderViewDto>>(ct);
        if (orders == null) return Ok(Array.Empty<OrderDto>());
        return Ok(orders.Select(o => o.ToDto()).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(Guid id, CancellationToken ct)
    {
        using var response = await ForwardRequestAsync(HttpMethod.Get, $"api/orders/{id}", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var order = await response.Content.ReadFromJsonAsync<OrderViewDto>(ct);
        return order == null ? NotFound() : Ok(order.ToDto());
    }

    [HttpGet("{id:guid}/workflow")]
    public async Task<IActionResult> GetOrderWorkflow(Guid id, CancellationToken ct)
    {
        using var response = await ForwardRequestAsync(HttpMethod.Get, $"api/orders/{id}/workflow", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        return Content(content, "application/json");
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(CreateOrderRequest request, CancellationToken ct)
    {
        using var response = await ForwardRequestAsync(HttpMethod.Post, "api/orders", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var order = await response.Content.ReadFromJsonAsync<OrderViewDto>(ct);
        return order == null ? NotFound() : CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderDto>> UpdateOrder(Guid id, UpdateOrderRequest request, CancellationToken ct)
    {
        using var response = await ForwardRequestAsync(HttpMethod.Put, $"api/orders/{id}", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var order = await response.Content.ReadFromJsonAsync<OrderViewDto>(ct);
        return order == null ? NotFound() : Ok(order.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteOrder(Guid id, CancellationToken ct)
    {
        using var response = await ForwardRequestAsync(HttpMethod.Delete, $"api/orders/{id}", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        return NoContent();
    }
}
