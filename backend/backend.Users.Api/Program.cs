using System.Net;
using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.ServiceDefaults;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Users;
using backend.Shared.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(backend.Users.Requests.Users.CreateUserCommand).Assembly));

// Configure database connections - use dedicated connection strings per service
var authDbConnectionString = builder.Configuration.GetConnectionString("Auth");

if (string.IsNullOrWhiteSpace(authDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Auth' is missing for backend.Users.Api.");
}

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(authDbConnectionString)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

var authAuthority = builder.Configuration["Auth:Authority"];
if (string.IsNullOrWhiteSpace(authAuthority))
{
    throw new InvalidOperationException("Auth:Authority is missing. Configure it in appsettings.json.");
}

var authAudience = builder.Configuration["Auth:Audience"];
builder.Services.AddJwtBearerAuthentication(authAuthority, authAudience);

builder.Services.AddAuthorization();

builder.Services.AddScoped<IUserDirectory, EfUserDirectory>();
builder.Services.AddScoped<IEffectiveUserAccessor, EffectiveUserAccessor>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHttpContextAccessor();

var sbConnStr = builder.Configuration["ServiceBus:ConnectionString"];
if (!string.IsNullOrWhiteSpace(sbConnStr))
{
    builder.Services.AddSingleton(_ => new Azure.Messaging.ServiceBus.ServiceBusClient(sbConnStr));
    builder.Services.AddSingleton<IOutboxPublisher, ServiceBusOutboxPublisher>();
    builder.Services.AddHostedService<OutboxDispatcher<AuthDbContext>>();
}
else
{
    // Register RabbitMQ connection factory
    builder.Services.AddSingleton<RabbitMqConnectionFactory>();
    builder.Services.Configure<backend.Shared.Configuration.RabbitMqOptions>(builder.Configuration.GetSection(backend.Shared.Configuration.RabbitMqOptions.SectionName));

    if (builder.Configuration.GetValue<bool>("RabbitMq:Enabled", false))
    {
        builder.Services.AddSingleton<IOutboxPublisher, RabbitMqOutboxPublisher>();
        builder.Services.AddHostedService<OutboxDispatcher<AuthDbContext>>();
    }
}

// Register outbox for users service (uses AuthDbContext)
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox<AuthDbContext>>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("default", opt =>
    {
        opt.PermitLimit = 50;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

// Apply pending migrations at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
    try
    {
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
        logger.LogInformation("AuthDbContext migrations applied.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "AuthDbContext migration failed.");
        throw;
    }
}

app.UseExceptionHandler();

// ACA terminates TLS and forwards X-Forwarded-* headers
#pragma warning disable ASPDEPR005
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapDefaultEndpoints();

app.Run();
