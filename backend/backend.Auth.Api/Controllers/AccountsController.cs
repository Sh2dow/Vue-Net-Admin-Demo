using backend.Domain.Data;
using backend.Domain.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace backend.Auth.Api.Controllers;

/// <summary>
/// Account management endpoints: login, logout, register.
/// For authorization code flow (browser-based) — login/logout return JSON or redirect.
/// </summary>
[ApiController]
[Route("account")]
public sealed class AccountsController : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? return_url)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return return_url is null
                ? Redirect("/")
                : LocalRedirect(return_url);
        }

        // API-only: return JSON with the return_url for the frontend to handle
        return Ok(new { return_url });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromForm] string username,
        [FromForm] string password,
        [FromForm] string? return_url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(string.Empty, "Username and password are required.");
            return BadRequest(ModelState);
        }

        var result = await AuthenticateAsync(username, password, cancellationToken);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return BadRequest(ModelState);
        }

        var principal = await CreatePrincipalAsync(result, cancellationToken);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(1))
            });

        return return_url is null
            ? Redirect("/")
            : LocalRedirect(return_url);
    }

    [HttpGet("logout")]
    public IActionResult Logout([FromQuery] string? return_url)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return return_url is null
                ? Redirect("/")
                : LocalRedirect(return_url);
        }

        // API-only: return JSON
        return Ok(new { return_url });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> LogoutPost([FromForm] string? return_url)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // 7.x: ForgetAllOpenIddictCookiesAsync removed — sign out the OpenIddict scheme too
            await HttpContext.SignOutAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return return_url is null
            ? Redirect("/")
            : LocalRedirect(return_url);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var dbContext = HttpContext.RequestServices.GetRequiredService<AuthDbContext>();

        var existing = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (existing is not null)
        {
            return Conflict(new { error = "Username already exists." });
        }

        var user = new AppUser
        {
            Subject = Guid.NewGuid().ToString("N"),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Roles = string.Join(",", request.Roles ?? Array.Empty<string>())
        };

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            roles = user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
        });
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var dbContext = HttpContext.RequestServices.GetRequiredService<AuthDbContext>();
        var user = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            roles = user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries),
            created_at = user.CreatedAtUtc
        });
    }

    [HttpGet("profile")]
    [Authorize]
    public IActionResult Profile()
    {
        return Ok(new
        {
            username = User.FindFirst(ClaimTypes.Name)?.Value,
            email = User.FindFirst(ClaimTypes.Email)?.Value,
            roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
        });
    }

    private async Task<AppUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var dbContext = HttpContext.RequestServices.GetRequiredService<AuthDbContext>();

        var user = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null || user.PasswordHash is null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }

    /// <summary>
    /// Creates a ClaimsPrincipal for the user with scopes set.
    /// 7.x: SignInDescriptor and CreateOpenIddictPrincipalAsync removed — use ClaimsIdentity directly.
    /// </summary>
    private Task<ClaimsPrincipal> CreatePrincipalAsync(AppUser user, CancellationToken cancellationToken)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Subject));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));
        identity.AddClaim(new Claim("preferred_username", user.Username));

        if (!string.IsNullOrEmpty(user.Email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrEmpty(user.Roles))
        {
            var roles = user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var role in roles)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        var principal = new ClaimsPrincipal(identity);

        // Set scopes for the userinfo endpoint
        principal.SetScopes(new[]
        {
            "openid",
            "profile",
            "email",
            "roles",
            "offline_access"
        });

        return Task.FromResult(principal);
    }
}

public sealed record RegisterRequest(
    string Username,
    string Password,
    string? Email,
    string[]? Roles);
