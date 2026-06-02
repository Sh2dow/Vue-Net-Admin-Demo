using backend.Domain.Models;
using backend.Shared.Application.Users;
using Microsoft.AspNetCore.Mvc;

namespace backend.Auth.Api.Controllers;

[ApiController]
[Route("internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    private readonly IUserDirectory _userDirectory;

    public InternalUsersController(IUserDirectory userDirectory)
    {
        _userDirectory = userDirectory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuthUserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _userDirectory.ListAsync(ct);
        return Ok(users.Select(ToDto).ToList());
    }

    [HttpGet("batch")]
    public async Task<ActionResult<IReadOnlyList<AuthUserDto>>> GetByIds([FromQuery] Guid[] ids, CancellationToken ct)
    {
        var users = await _userDirectory.GetByIdsAsync(ids, ct);
        return Ok(users.Values.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuthUserDto>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userDirectory.FindByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    [HttpGet("by-subject/{subject}")]
    public async Task<ActionResult<AuthUserDto>> GetBySubject(string subject, CancellationToken ct)
    {
        var user = await _userDirectory.FindBySubjectAsync(subject, ct);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    [HttpPost("ensure")]
    public async Task<ActionResult<AuthUserDto>> Ensure([FromBody] EnsureAuthUserRequest request, CancellationToken ct)
    {
        var user = await _userDirectory.EnsureAsync(
            request.Subject.Trim(),
            request.PreferredUsername?.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            ct);
        return Ok(ToDto(user));
    }

    [HttpPost]
    public async Task<ActionResult<AuthUserDto>> Create([FromBody] CreateAuthUserRequest request, CancellationToken ct)
    {
        var existing = await _userDirectory.FindBySubjectAsync(request.Subject.Trim(), ct);
        if (existing != null)
        {
            return Conflict("User with this subject already exists.");
        }

        var created = await _userDirectory.CreateAsync(
            new AppUser
            {
                Subject = request.Subject.Trim(),
                Username = request.Username.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim()
            },
            ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AuthUserDto>> Update(Guid id, [FromBody] UpdateAuthUserRequest request, CancellationToken ct)
    {
        var user = await _userDirectory.FindByIdAsync(id, ct);
        if (user == null)
        {
            return NotFound();
        }

        user.Username = request.Username.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        var updated = await _userDirectory.UpdateAsync(user, ct);
        return Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var affected = await _userDirectory.DeleteByIdAsync(id, ct);
        return affected > 0 ? NoContent() : NotFound();
    }

    private static AuthUserDto ToDto(AppUser user) =>
        new(user.Id, user.Subject, user.Username, user.Email, user.CreatedAtUtc);
}
