using backend.Auth.Api;
using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.ServiceDefaults;
using backend.Shared.Application.Users;
using backend.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

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
        .UseOpenIddict();
});

builder.Services.AddScoped<IUserDirectory, EfUserDirectory>();

// ---------------------------------------------------------------------------
// OpenIddict server configuration (7.x API)
// ---------------------------------------------------------------------------
builder.Services.AddOpenIddict()
    // 7.x: AddCore(Action<OpenIddictCoreBuilder>) returns OpenIddictBuilder, UseEntityFrameworkCore is on CoreBuilder
    .AddCore(core => core.UseEntityFrameworkCore(ef => ef.UseDbContext<AuthDbContext>()))
    .AddServer(server =>
    {
        server.SetIssuer(new Uri(builder.Configuration.GetValue<string>("Auth:Issuer") ?? "https://localhost:5201"));

        // 7.x: RegisterScopes takes params string[]
        server.RegisterScopes("openid", "profile", "email", "roles", "offline_access");

        // Enable endpoints
        server.SetAuthorizationEndpointUris(new Uri("/connect/authorize", UriKind.Relative));
        server.SetTokenEndpointUris(new Uri("/connect/token", UriKind.Relative));
        server.SetIntrospectionEndpointUris(new Uri("/connect/introspect", UriKind.Relative));
        server.SetUserInfoEndpointUris(new Uri("/connect/userinfo", UriKind.Relative));
        server.SetEndSessionEndpointUris(new Uri("/connect/logout", UriKind.Relative));

        // Enable flows
        server.AllowAuthorizationCodeFlow()
            .AllowPasswordFlow()
            .AllowRefreshTokenFlow();

        // Token settings
        server.SetAccessTokenLifetime(TimeSpan.FromHours(1))
            .SetRefreshTokenLifetime(TimeSpan.FromDays(30));

        // Development certificates
        server.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        // AspNetCore integration — passthrough lets us render a custom login page
        server.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough();

        // Register password grant handler (OpenIddict 7.x event model)
        server.AddEventHandler<OpenIddictServerEvents.HandleTokenRequestContext>(builder =>
        {
            builder.AddFilter<RequirePasswordGrantFilter>()
                .UseScopedHandler<PasswordGrantHandler>();
        });
    });

// Authentication: OpenIddict server handler cannot be ANY default scheme.
// Use cookies for default/sign-in; no default challenge — OpenIddict handles /connect/* natively.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie();

builder.Services.AddAuthorization();

// CORS for dev
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", p => p
        .WithOrigins(
            "http://localhost:5173",
            "http://localhost:3000"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Run seeding synchronously before accepting requests (prevents race condition with OIDC)
using (var scope = app.Services.CreateScope())
{
    var seed = new SeedData(
        app.Services,
        scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SeedData>>()
    );
    await seed.StartAsync(CancellationToken.None);
}

app.UseCors("dev");       // First: CORS preflight (OPTIONS) must not be swallowed by exception handler
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "auth-api" }));

app.Run();
