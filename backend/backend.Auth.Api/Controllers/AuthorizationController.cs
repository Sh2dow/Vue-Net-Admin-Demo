using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using System.Security.Claims;
using System.Web;

namespace backend.Auth.Api.Controllers;

/// <summary>
/// Handles POST /login — validates credentials, sets cookie, redirects back to returnUrl.
/// The /connect/authorize endpoint (MapMethods) renders the login form for unauthenticated users.
/// </summary>
[Route("login")]
[AllowAnonymous]
public class AuthorizationController : ControllerBase
{
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(ILogger<AuthorizationController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        _logger.LogInformation("Login POST: returnUrl = {ReturnUrl}", returnUrl);

        if (username != "admin" || password != "Admin@123")
        {
            return Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.Name, "admin"));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "admin"));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.PreferredUsername, "admin"));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, "admin@localhost"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "admin"));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
            });

        // After setting the cookie, redirect back to the returnUrl so the /connect/authorize
        // endpoint re-runs and sees the authenticated user via UseAuthentication.
        var target = !string.IsNullOrWhiteSpace(returnUrl)
            ? returnUrl
            : "/";

        _logger.LogInformation("Redirecting to: {Target}", target);
        return Redirect(target);
    }
}
