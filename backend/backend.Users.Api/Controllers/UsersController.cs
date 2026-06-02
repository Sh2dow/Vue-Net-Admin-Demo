using backend.Domain.Data;
using backend.Shared.Application.Users;
using backend.Users.Dtos;
using backend.Users.Requests.Users;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace backend.Users.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserWithOrdersDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _sender.Send(new GetUsersQuery(), ct);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserWithOrdersDto>> GetUserById(Guid id, CancellationToken ct)
    {
        var user = await _sender.Send(new GetUserByIdQuery(id), ct);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserWithOrdersDto>> CreateUser(CreateUserCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetUserById), new { id = result.Value.Id }, result.Value) : BadRequest(result.Errors);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserWithOrdersDto>> UpdateUser(Guid id, UpdateUserCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new DeleteUserCommand(id), ct);
        return result.IsSuccess ? NoContent() : NotFound();
    }
}
