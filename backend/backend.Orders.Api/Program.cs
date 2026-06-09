using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Database;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Orders.Infrastructure.Orders;
using backend.Orders.Validation.Orders;
using backend.ServiceDefaults;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Users;
using backend.Shared.Configuration;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<CreateDigitalOrderCommandValidator>();
builder.Services.AddScoped<CreatePhysicalOrderCommandValidator>();
builder.Services.AddScoped<CreateOrderCommandValidator>();
builder.Services.AddScoped<UpdateOrderCommandValidator>();

var authAuthority = builder.Configuration["Auth:Authority"];
if (string.IsNullOrWhiteSpace(authAuthority))
{
    throw new InvalidOperationException("Auth:Authority is missing. Configure it in appsettings.json.");
}

builder.Services.AddJwtBearerAuthentication(authAuthority);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = null;
});

// Register MediatR handlers from the feature assembly
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(backend.Orders.Requests.Orders.CreateDigitalOrderCommand).Assembly));

// Configure database connections - use dedicated connection strings per service
var ordersDbConnectionString = builder.Configuration.GetConnectionString("Orders");
var paymentsDbConnectionString = builder.Configuration.GetConnectionString("Payments");

if (string.IsNullOrWhiteSpace(ordersDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Orders' is missing for backend.Orders.Api.");
}

if (string.IsNullOrWhiteSpace(paymentsDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Payments' is missing for backend.Orders.Api.");
}

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(ordersDbConnectionString));

builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseSqlServer(paymentsDbConnectionString));

builder.Services.Configure<AuthServiceOptions>(builder.Configuration.GetSection(AuthServiceOptions.SectionName));

builder.Services.AddHttpClient<IUserDirectory, HttpUserDirectory>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AuthServiceOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        throw new InvalidOperationException(
            $"{AuthServiceOptions.SectionName}:BaseUrl is missing. Configure it in appsettings.json or provide it via environment variables.");
    }

    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<IEffectiveUserAccessor, EffectiveUserAccessor>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHttpContextAccessor();

var sbConnStr = builder.Configuration["ServiceBus:ConnectionString"];
if (!string.IsNullOrWhiteSpace(sbConnStr))
{
    builder.Services.AddSingleton(_ => new Azure.Messaging.ServiceBus.ServiceBusClient(sbConnStr));
    builder.Services.AddSingleton<IOutboxPublisher, ServiceBusOutboxPublisher>();
    builder.Services.AddHostedService<OutboxDispatcher<OrdersDbContext>>();
    builder.Services.AddHostedService<ServiceBusOrderSagaConsumer>();
    builder.Services.AddHostedService<ServiceBusOrderExecutionDispatchConsumer>();
}
else
{
    // Register RabbitMQ connection factory
    builder.Services.AddSingleton<RabbitMqConnectionFactory>();
    builder.Services.Configure<backend.Shared.Configuration.RabbitMqOptions>(builder.Configuration.GetSection(backend.Shared.Configuration.RabbitMqOptions.SectionName));

    // Override RabbitMQ URI from environment variable if available
    builder.Services.PostConfigure<backend.Shared.Configuration.RabbitMqOptions>(options =>
    {
        var aspireConnectionString = builder.Configuration.GetConnectionString("messaging");
        if (!string.IsNullOrWhiteSpace(aspireConnectionString))
        {
            options.Uri = aspireConnectionString;
        }
    });

    if (builder.Configuration.GetValue<bool>("RabbitMq:Enabled", false))
    {
        builder.Services.AddSingleton<IOutboxPublisher, RabbitMqOutboxPublisher>();
        builder.Services.AddHostedService<OutboxDispatcher<OrdersDbContext>>();
        builder.Services.AddHostedService<OrderSagaConsumer>();
        builder.Services.AddHostedService<OrderExecutionDispatchConsumer>();
    }
}

// Register outbox for orders service (uses OrdersDbContext)
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox<OrdersDbContext>>();

builder.Services.AddDatabaseMigration<OrdersDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapDefaultEndpoints();

app.Run();
