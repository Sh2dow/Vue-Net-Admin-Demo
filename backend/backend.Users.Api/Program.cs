using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.ServiceDefaults;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Users;
using backend.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.Extensions.Options;
using RabbitMqConnectionFactory = backend.Infrastructure.Infrastructure.Messaging.RabbitMqConnectionFactory;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(backend.Users.Requests.Users.CreateUserCommand).Assembly));

// Configure database connections - use dedicated connection strings per service
var authDbConnectionString = builder.Configuration.GetConnectionString("Auth");
var ordersDbConnectionString = builder.Configuration.GetConnectionString("Orders");

if (string.IsNullOrWhiteSpace(authDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Auth' is missing for backend.Users.Api.");
}

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(authDbConnectionString)
        .UseSnakeCaseNamingConvention());

if (!string.IsNullOrWhiteSpace(ordersDbConnectionString))
{
    builder.Services.AddDbContext<OrdersDbContext>(options =>
        options.UseNpgsql(ordersDbConnectionString)
            .UseSnakeCaseNamingConvention());
}

builder.Services.AddScoped<IUserDirectory, EfUserDirectory>();

// Register RabbitMQ connection factory
builder.Services.AddSingleton<RabbitMqConnectionFactory>();
builder.Services.Configure<backend.Shared.Configuration.RabbitMqOptions>(builder.Configuration.GetSection(backend.Shared.Configuration.RabbitMqOptions.SectionName));

// Register outbox for users service (uses AuthDbContext)
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox<AuthDbContext>>();
if (builder.Configuration.GetValue<bool>("RabbitMq:Enabled", true))
{
    builder.Services.AddHostedService<OutboxDispatcher<AuthDbContext>>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
