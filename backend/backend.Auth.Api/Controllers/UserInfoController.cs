using backend.Domain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Auth.Api.Controllers;

/// <summary>
/// OpenID Connect userinfo endpoint. Returns the authenticated user's profile.
/// </summary>
[ApiController]
[Route("connect")]
[Authorize]
public sealed class UserInfoController : ControllerBase
{
    [HttpGet("userinfo")]
    public async Task<IActionResult> GetUserInfo(CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject)?.Value;

        if (string.IsNullOrEmpty(subject))
        {
            return Unauthorized();
        }

        var dbContext = HttpContext.RequestServices.GetRequiredService<AuthDbContext>();
        var user = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Subject == subject, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var roles = string.IsNullOrEmpty(user.Roles)
            ? Array.Empty<string>()
            : user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Ok(new
        {
            sub = user.Subject,
            name = user.Username,
            preferred_username = user.Username,
            email = user.Email,
            roles = roles
        });
    }
}
