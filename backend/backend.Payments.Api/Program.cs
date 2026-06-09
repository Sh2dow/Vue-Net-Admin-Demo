using System.Net;
using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Database;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Payments.Infrastructure.Payments;
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

var authAuthority = builder.Configuration["Auth:Authority"];
if (string.IsNullOrWhiteSpace(authAuthority))
{
    throw new InvalidOperationException("Auth:Authority is missing. Configure it in appsettings.json.");
}

var authAudience = builder.Configuration["Auth:Audience"];
builder.Services.AddJwtBearerAuthentication(authAuthority, authAudience);

builder.Services.AddAuthorization();

builder.Services.Configure<backend.Shared.Configuration.RabbitMqOptions>(builder.Configuration.GetSection(backend.Shared.Configuration.RabbitMqOptions.SectionName));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));

// Configure database connections - use dedicated connection strings per service
var ordersDbConnectionString = builder.Configuration.GetConnectionString("Orders");
var paymentsDbConnectionString = builder.Configuration.GetConnectionString("Payments");
var authDbConnectionString = builder.Configuration.GetConnectionString("Auth");

if (string.IsNullOrWhiteSpace(paymentsDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Payments' is missing for backend.Payments.Api.");
}

if (string.IsNullOrWhiteSpace(authDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Auth' is missing for backend.Payments.Api.");
}

if (string.IsNullOrWhiteSpace(ordersDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Orders' is missing for backend.Payments.Api.");
}

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(ordersDbConnectionString));

builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseSqlServer(paymentsDbConnectionString));

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(authDbConnectionString));

builder.Services.AddScoped<IUserDirectory, EfUserDirectory>();
builder.Services.AddScoped<IEffectiveUserAccessor, EffectiveUserAccessor>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHttpContextAccessor();

var sbConnStr = builder.Configuration["ServiceBus:ConnectionString"];
if (!string.IsNullOrWhiteSpace(sbConnStr))
{
    builder.Services.AddSingleton(_ => new Azure.Messaging.ServiceBus.ServiceBusClient(sbConnStr));
    builder.Services.AddSingleton<IOutboxPublisher, ServiceBusOutboxPublisher>();
    builder.Services.AddHostedService<OutboxDispatcher<OrdersDbContext>>();
    builder.Services.AddHostedService<ServiceBusPaymentStubConsumer>();
}
else
{
    // Register RabbitMQ connection factory
    builder.Services.AddSingleton<RabbitMqConnectionFactory>();

    if (builder.Configuration.GetValue<bool>("RabbitMq:Enabled", false))
    {
        builder.Services.AddSingleton<IOutboxPublisher, RabbitMqOutboxPublisher>();
        builder.Services.AddHostedService<OutboxDispatcher<OrdersDbContext>>();
        builder.Services.AddHostedService<PaymentStubConsumer>();
    }
}

// Register outbox for payments service (uses OrdersDbContext for saga state)
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox<OrdersDbContext>>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("default", opt =>
    {
        opt.PermitLimit = 50;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

builder.Services.AddDatabaseMigration<PaymentsDbContext>();

var app = builder.Build();

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
app.MapDefaultEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "payments-api" }));

app.Run();
