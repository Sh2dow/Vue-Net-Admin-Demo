using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.Payments.Infrastructure.Payments;
using backend.ServiceDefaults;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Users;
using backend.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

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

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(ordersDbConnectionString ?? throw new InvalidOperationException("Connection string 'Orders' is missing for backend.Payments.Api."))
        .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(paymentsDbConnectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(authDbConnectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IUserDirectory, EfUserDirectory>();
builder.Services.AddScoped<IEffectiveUserAccessor, EffectiveUserAccessor>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHttpContextAccessor();

// Register RabbitMQ connection factory
builder.Services.AddSingleton<RabbitMqConnectionFactory>();

// Register outbox for payments service (uses OrdersDbContext for saga state)
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox<OrdersDbContext>>();
if (builder.Configuration.GetValue<bool>("RabbitMq:Enabled", true))
{
    builder.Services.AddHostedService<OutboxDispatcher<OrdersDbContext>>();
    builder.Services.AddHostedService<PaymentStubConsumer>();
}

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    if (await DatabaseExistsAsync(paymentsDbConnectionString))
    {
        await services.GetRequiredService<PaymentsDbContext>().Database.EnsureCreatedAsync();
    }
    else
    {
        logger.LogWarning("Skipping PaymentsDbContext migration because database '{Database}' does not exist or is not visible to the current PostgreSQL role.", new NpgsqlConnectionStringBuilder(paymentsDbConnectionString).Database);
    }
}

static async Task<bool> DatabaseExistsAsync(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Database = "postgres"
    };

    await using var connection = new NpgsqlConnection(builder.ConnectionString);

    try
    {
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @databaseName", connection);
        command.Parameters.AddWithValue("databaseName", new NpgsqlConnectionStringBuilder(connectionString).Database ?? string.Empty);

        return await command.ExecuteScalarAsync() is not null;
    }
    catch (PostgresException ex) when (ex.SqlState is "42501" or "3D000")
    {
        return false;
    }
}

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
