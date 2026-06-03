using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace backend.Auth.Api.Controllers;

/// <summary>
/// Handles the authorization endpoint passthrough — renders a login form and processes credentials.
/// </summary>
[Route("connect")]
public class AuthorizationController : ControllerBase
{
    private const string RedirectUriCookieName = ".auth_redirect_uri";

    [HttpGet("authorize")]
    public IActionResult Get()
    {
        // If the user is already authenticated, let OpenIddict handle the request (automatic grant).
        if (User.Identity?.IsAuthenticated == true)
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = "/connect/authorize" },
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // Store the original authorization request URI so we can redirect back after login
        HttpContext.Response.Cookies.Append(
            RedirectUriCookieName,
            Request.PathBase + Request.Path + Request.QueryString,
            new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict });

        return Content(BuildLoginForm(), "text/html");
    }

    [HttpPost("login")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        // Validate the credentials
        if (username != "admin" || password != "Admin@123")
        {
            return Content(BuildLoginForm("Invalid credentials. Try admin/Admin@123"), "text/html");
        }

        // Create the principal
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "1"));
        identity.AddClaim(new Claim(ClaimTypes.Name, "admin"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "admin"));

        // Sign in with cookie
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        // Retrieve the original authorization request URI and redirect to it
        var redirectUri = HttpContext.Request.Cookies[RedirectUriCookieName] ?? "/connect/authorize";
        HttpContext.Response.Cookies.Delete(RedirectUriCookieName);

        return Redirect(redirectUri);
    }

    private static string BuildLoginForm(string? error = null)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Sign in - Vue Admin Demo</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; display: flex; justify-content: center; align-items: center; min-height: 100vh; }}
        .login-card {{ background: white; padding: 40px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); width: 100%; max-width: 400px; }}
        h1 {{ text-align: center; margin-bottom: 8px; color: #333; }}
        .subtitle {{ text-align: center; color: #666; margin-bottom: 24px; font-size: 14px; }}
        label {{ display: block; margin-bottom: 4px; font-weight: 500; color: #333; font-size: 14px; }}
        input[type=""text""], input[type=""password""] {{ width: 100%; padding: 10px 12px; border: 1px solid #ddd; border-radius: 4px; margin-bottom: 16px; font-size: 14px; }}
        input[type=""text""]:focus, input[type=""password""]:focus {{ outline: none; border-color: #1976d2; box-shadow: 0 0 0 2px rgba(25,118,210,0.2); }}
        button {{ width: 100%; padding: 12px; background: #1976d2; color: white; border: none; border-radius: 4px; font-size: 16px; cursor: pointer; font-weight: 500; }}
        button:hover {{ background: #1565c0; }}
        .error {{ background: #ffebee; color: #c62828; padding: 10px; border-radius: 4px; margin-bottom: 16px; font-size: 14px; text-align: center; }}
    </style>
</head>
<body>
    <div class=""login-card"">
        <h1>Sign in</h1>
        <p class=""subtitle"">Vue .NET Admin Demo</p>
        {(error != null ? $"<div class=\"error\">{error}</div>" : "")}
        <form method=""post"" action=""/connect/login"">
            <label for=""username"">Username</label>
            <input type=""text"" id=""username"" name=""username"" required autofocus>
            <label for=""password"">Password</label>
            <input type=""password"" id=""password"" name=""password"" required>
            <button type=""submit"">Sign in</button>
        </form>
    </div>
</body>
</html>";
    }
}
