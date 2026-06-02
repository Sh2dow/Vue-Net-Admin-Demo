using backend.Domain.Data;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace backend.Auth.Api;

/// <summary>
/// Handles the password grant token requests by validating user credentials.
/// Registered in Program.cs via AddEventHandler&lt;OpenIddictServerEvents.HandleTokenRequestContext&gt;().
/// </summary>
public sealed class PasswordGrantHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly AuthDbContext _dbContext;

    public PasswordGrantHandler(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        var username = context.Request.Username;
        var password = context.Request.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new OpenIddictExceptions.ProtocolException(
                "invalid_grant",
                "The username and password parameters are required.");
        }

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, CancellationToken.None);

        if (user is null || user.PasswordHash is null)
        {
            throw new OpenIddictExceptions.ProtocolException(
                "invalid_grant",
                "Invalid username or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            throw new OpenIddictExceptions.ProtocolException(
                "invalid_grant",
                "Invalid username or password.");
        }

        // Create the ClaimsPrincipal manually (7.x: no CreateUserPrincipal or SignInDescriptor)
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Subject));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));

        if (!string.IsNullOrEmpty(user.Email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        }

        // Add roles as claims
        if (!string.IsNullOrEmpty(user.Roles))
        {
            var roles = user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var role in roles)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        var principal = new ClaimsPrincipal(identity);

        // Set scopes on the principal (7.x: SetScopes takes string[])
        var requestedScopes = context.Request.GetScopes().ToList();
        principal.SetScopes(requestedScopes.ToArray());

        // Sign in the principal — this creates the token entries and triggers token generation.
        // If offline_access is in the scopes, OpenIddict automatically generates a refresh token.
        context.SignIn(principal);
    }
}

/// <summary>
/// Filter to only activate this handler for password grant type requests.
/// Uses context.Request.IsPasswordGrantType() extension method (7.x API).
/// </summary>
public sealed class RequirePasswordGrantFilter : IOpenIddictServerHandlerFilter<OpenIddictServerEvents.HandleTokenRequestContext>
{
    public ValueTask<bool> IsActiveAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        // 7.x: IsPasswordGrantType() is an extension on OpenIddictRequest, not on the context
        return new(context.Request.IsPasswordGrantType());
    }
}
