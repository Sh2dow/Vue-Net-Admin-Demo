using backend.Domain.Data;
using backend.Orders.Dtos;
using backend.Orders.Mappers;
using backend.Orders.Requests.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Orders.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<OrdersController> _logger;
    private readonly IConfiguration _configuration;

    public OrdersController(ISender sender, ILogger<OrdersController> logger, IConfiguration configuration)
    {
        _sender = sender;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderViewDto>>> GetOrders(CancellationToken ct)
    {
        var hasAuth = Request.Headers.TryGetValue("Authorization", out var authVal);
        _logger.LogInformation(
            "GetOrders called. QueryString={QueryString}, HasAuthHeader={HasAuth}, AuthPrefix={AuthPrefix}, IsAuthenticated={IsAuth}, UserName={UserName}",
            Request.QueryString.Value,
            hasAuth,
            hasAuth ? authVal.ToString()[..Math.Min(20, authVal.ToString().Length)] + "..." : null,
            User.Identity?.IsAuthenticated,
            User.Identity?.Name);
        var result = await _sender.Send(new GetOrdersQuery(), ct);
        return Ok(result);
    }

    [HttpGet("debug/all")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetAllOrders(CancellationToken ct)
    {
        var orders = await _sender.Send(new GetAllOrdersQuery(), ct);
        return Ok(orders);
    }

    [HttpGet("debug/auth")]
    public ActionResult<object> DebugAuth()
    {
        var authHeader = Request.Headers.TryGetValue("Authorization", out var h) ? h.ToString() : null;
        var identity = User.Identity;
        return Ok(new
        {
            hasAuthHeader = !string.IsNullOrWhiteSpace(authHeader),
            authHeaderPrefix = authHeader?.Length > 20 ? authHeader[..20] + "..." : authHeader,
            isAuthenticated = identity?.IsAuthenticated ?? false,
            name = identity?.Name,
            subject = User.FindFirst("sub")?.Value,
            preferredUsername = User.FindFirst("preferred_username")?.Value,
            email = User.FindFirst("email")?.Value,
            roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList(),
            asUserId = Request.Query["asUserId"].FirstOrDefault(),
        });
    }

    [HttpGet("debug/jwt")]
    public async Task<ActionResult<object>> DebugJwt()
    {
        var authHeader = Request.Headers.TryGetValue("Authorization", out var h) ? h.ToString() : null;
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Ok(new { tokenPresent = false });
        }

        var token = authHeader["Bearer ".Length..];
        var authority = _configuration["Auth:Authority"]?.TrimEnd('/');
        var metadataAddress = _configuration["Auth:MetadataAddress"];
        if (string.IsNullOrWhiteSpace(metadataAddress))
        {
            metadataAddress = $"{authority}/.well-known/openid-configuration";
        }

        string? validationError = null;
        try
        {
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
            using var http = new HttpClient(handler);
            var config = await http.GetStringAsync(metadataAddress);
            var doc = System.Text.Json.JsonDocument.Parse(config);
            var jwksUri = doc.RootElement.GetProperty("jwks_uri").GetString();
            var jwks = await http.GetStringAsync(jwksUri!);

            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = true,
                ValidIssuer = authority,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                IssuerSigningKeys = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(jwks).GetSigningKeys(),
                NameClaimType = "sub",
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };

            tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex)
        {
            validationError = ex.ToString();
        }

        return Ok(new
        {
            tokenPresent = true,
            authority,
            metadataAddress,
            validationError,
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderViewDto>> GetOrderById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetOrderByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/workflow")]
    public async Task<ActionResult<OrderWorkflowDto>> GetOrderWorkflow(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetOrderWorkflowQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<OrderTimelineItemDto>>> GetOrderTimeline(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetOrderTimelineQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<OrderViewDto>> CreateOrder(CreateOrderCommand command, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Creating order: {OrderType}, {TotalAmount}", command.OrderType, command.TotalAmount);

            var result = await _sender.Send(command, ct);
            
            if (!result.IsSuccess)
            {
                return BadRequest(result.Errors);
            }

            return CreatedAtAction(nameof(GetOrderById), new { id = result.Value.Id }, result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            throw;
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderViewDto>> UpdateOrder(Guid id, UpdateOrderCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteOrder(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new DeleteOrderCommand(id), ct);
        return result.Value ? NoContent() : NotFound();
    }
}
