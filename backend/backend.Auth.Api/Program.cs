using System.Net;
using System.Security.Claims;
using System.Text.Json;
using backend.Auth.Api;
using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.ServiceDefaults;
using backend.Shared.Application.Users;
using backend.Shared.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// AddServiceDefaults enables HTTPS with dev cert (required by OpenIddict)
builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// Configure strongly-typed options from configuration
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

// Configure database connection
var authConnectionString = builder.Configuration.GetConnectionString("Auth");
if (string.IsNullOrWhiteSpace(authConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Auth' is missing for backend.Auth.Api. " +
        "Set ConnectionStrings__Auth in environment variables.");
}

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseNpgsql(authConnectionString)
        .UseSnakeCaseNamingConvention()
        .UseOpenIddict()
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddScoped<IUserDirectory, EfUserDirectory>();

// ---------------------------------------------------------------------------
// OpenIddict server configuration (7.x API)
// ---------------------------------------------------------------------------
builder.Services.AddOpenIddict()
    .AddCore(core => core.UseEntityFrameworkCore(ef => ef.UseDbContext<AuthDbContext>()))
    .AddServer(server =>
    {
        server.SetIssuer(new Uri(builder.Configuration.GetValue<string>("Auth:Issuer") ?? "https://localhost:5201"));

        server.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, "roles", "offline_access");

        // Endpoints
        server.SetAuthorizationEndpointUris(new Uri("/connect/authorize", UriKind.Relative));
        server.SetTokenEndpointUris(new Uri("/connect/token", UriKind.Relative));
        server.SetIntrospectionEndpointUris(new Uri("/connect/introspect", UriKind.Relative));
        server.SetUserInfoEndpointUris(new Uri("/connect/userinfo", UriKind.Relative));
        server.SetEndSessionEndpointUris(new Uri("/connect/logout", UriKind.Relative));

        // Flows
        server.AllowAuthorizationCodeFlow()
            .AllowPasswordFlow()
            .AllowRefreshTokenFlow();
        server.RequireProofKeyForCodeExchange();

        server.SetAccessTokenLifetime(TimeSpan.FromHours(1))
            .SetRefreshTokenLifetime(TimeSpan.FromDays(30));

        // Development certificates
        server.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        // Passthrough: lets our MapMethods endpoint handle /connect/authorize
        server.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();
    });

// Authentication: Cookie for session
// SameSite=None + Secure=Always so the cookie survives cross-port redirects
// from the frontend (localhost:5173) to Auth.Api (localhost:5201).
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization();

// CORS — origins from configuration (override via env var: CORS__AllowedOrigins)
// Supports comma-separated string (shell-friendly) or JSON array
builder.Services.AddCors(options =>
{
    options.AddPolicy("cors", p =>
    {
        var corsBuilder = p.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        var raw = builder.Configuration.GetValue<string>("CORS:AllowedOrigins");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            // Try JSON array first, fall back to comma-separated
            var origins = raw.Trim().StartsWith('[')
                ? JsonSerializer.Deserialize<string[]>(raw) ?? Array.Empty<string>()
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (origins.Length > 0)
                corsBuilder.WithOrigins(origins);
            else
                corsBuilder.WithOrigins("http://localhost:5173", "http://localhost:3000");
        }
        else
        {
            corsBuilder.WithOrigins("http://localhost:5173", "http://localhost:3000");
        }
    });
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("token", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

// Apply migrations + seed data before accepting requests.
// OpenIddict must be able to read the database before serving /.well-known/openid-configuration.
// The try/catch in SeedData already logs warnings on failure (e.g. database not ready yet).
using (var scope = app.Services.CreateScope())
{
    var seed = new SeedData(
        app.Services,
        builder.Configuration,
        scope.ServiceProvider.GetRequiredService<ILogger<SeedData>>()
    );
    await seed.StartAsync(CancellationToken.None);
}

app.UseExceptionHandler();

// ACA terminates TLS at the ingress — app receives HTTP, so force HTTPS scheme in production.
if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        context.Request.Scheme = "https";
        await next();
    });
}

// Trust forwarded headers from ACA's internal ingress (10.0.0.0/8 VNet range).
#pragma warning disable ASPDEPR005 // KnownProxies deprecated but has no .NET 10 replacement
var fho = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedHost
                     | ForwardedHeaders.XForwardedProto,
};
fho.KnownIPNetworks.Clear();
fho.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 16));
fho.KnownProxies.Clear();
app.UseForwardedHeaders(fho);
#pragma warning restore ASPDEPR005

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseCors("cors");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapDefaultEndpoints();

// ---------------------------------------------------------------------------
// Authorization endpoint — Zirku pattern (MapMethods + passthrough)
// ---------------------------------------------------------------------------
app.MapMethods("connect/authorize", [HttpMethods.Get, HttpMethods.Post], async (
    HttpContext context,
    IOpenIddictScopeManager scopeManager) =>
{
    // 1. Retrieve the OpenIddict request
    var request = context.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

    // 2. Check if the user is already authenticated (cookie set by /login POST)
    if (context.User.Identity is { IsAuthenticated: true })
    {
        // 3. Build the claims identity for OpenIddict
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        // Zirku: explicitly add mandatory claims
        identity.AddClaim(new Claim(Claims.Subject,
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name
            ?? Guid.NewGuid().ToString("N")));
        identity.AddClaim(new Claim(Claims.Name, context.User.Identity?.Name ?? "admin"));

        // Copy remaining claims (roles, email, etc.) — skip claims already explicitly added
        foreach (var claim in context.User.Claims)
        {
            if (claim.Type == ClaimTypes.NameIdentifier || claim.Type == ClaimTypes.Name ||
                claim.Type == Claims.Subject || claim.Type == Claims.Name)
                continue;
            identity.AddClaim(new Claim(claim.Type, claim.Value));
        }

        // Grant all requested scopes
        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

        // Allow claims in access tokens; roles, preferred_username, email also in identity token
        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Role or Claims.PreferredUsername or Claims.Email or ClaimTypes.Role =>
                [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken]
        });

        return Results.SignIn(
            new ClaimsPrincipal(identity),
            properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // 4. Not authenticated — challenge to trigger redirect to /login
    // QueryString already includes the leading '?', so no extra '?' before it
    return Results.Challenge(
        authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme],
        properties: new AuthenticationProperties
        {
            RedirectUri = $"/connect/authorize{context.Request.QueryString}"
        });
});

// Login page — renders form at GET /login
app.MapGet("/login", (string? returnUrl) =>
{
    var target = !string.IsNullOrWhiteSpace(returnUrl)
        ? returnUrl
        : "/connect/authorize";
    return Results.Text(BuildLoginForm(target), "text/html");
});

// Logout endpoint — clears cookie and redirects to frontend
app.MapMethods("connect/logout", [HttpMethods.Get, HttpMethods.Post], (HttpContext context) =>
{
    var postLogoutUri = context.Request.Query["post_logout_redirect_uri"].FirstOrDefault()
        ?? builder.Configuration["Auth:PostLogoutRedirectUri"]
        ?? "/login";

    return Results.SignOut(
        authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme],
        properties: new AuthenticationProperties
        {
            RedirectUri = postLogoutUri
        });
});

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "auth-api" }));

app.Run();

// ---------------------------------------------------------------------------
// Login form HTML
// ---------------------------------------------------------------------------
static string BuildLoginForm(string returnUrl)
{
    var encoded = System.Web.HttpUtility.HtmlEncode(returnUrl);
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
        <form method=""post"" action=""/login"">
            <input type=""hidden"" name=""returnUrl"" value=""{encoded}"">
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
