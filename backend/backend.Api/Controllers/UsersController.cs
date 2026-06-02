using System.Net.Http.Json;
using backend.Users.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace backend.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public UsersController(IHttpClientFactory httpClientFactory)
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

        return await _httpClientFactory.CreateClient("Users").SendAsync(request, ct);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/users", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var users = await response.Content.ReadFromJsonAsync<IReadOnlyList<UserWithOrdersDto>>(ct);
        return Ok(users?.Select(u => u.ToDto()).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/users/{id}", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var user = await response.Content.ReadFromJsonAsync<UserWithOrdersDto>(ct);
        return user == null ? NotFound() : Ok(user.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/users", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var user = await response.Content.ReadFromJsonAsync<UserWithOrdersDto>(ct);
        return user == null ? NotFound() : CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Put, $"api/users/{id}", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var user = await response.Content.ReadFromJsonAsync<UserWithOrdersDto>(ct);
        return user == null ? NotFound() : Ok(user.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/users/{id}", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        return NoContent();
    }
}
