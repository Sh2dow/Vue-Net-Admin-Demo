using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.ServiceDefaults;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

builder.Services.AddJwtBearerAuthentication(authAuthority);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = null;
});

// Register MediatR handlers from the feature assembly
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(backend.Tasks.Requests.Tasks.CreateTaskCommand).Assembly));

// Configure database connections - use dedicated connection strings per service
var tasksDbConnectionString = builder.Configuration.GetConnectionString("Tasks");
var authDbConnectionString = builder.Configuration.GetConnectionString("Auth");

if (string.IsNullOrWhiteSpace(tasksDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Tasks' is missing for backend.Tasks.Api.");
}

if (string.IsNullOrWhiteSpace(authDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Auth' is missing for backend.Tasks.Api.");
}

builder.Services.AddDbContext<TasksDbContext>(options =>
    options.UseNpgsql(tasksDbConnectionString)
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

// Register outbox for tasks service
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox<TasksDbContext>>();
if (builder.Configuration.GetValue<bool>("RabbitMq:Enabled", true))
{
    builder.Services.AddHostedService<OutboxDispatcher<TasksDbContext>>();
}

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        if (await DatabaseExistsAsync(tasksDbConnectionString))
        {
            await services.GetRequiredService<TasksDbContext>().Database.EnsureCreatedAsync();
        }
        else
        {
            logger.LogWarning("Skipping TasksDbContext migration because database '{Database}' does not exist or is not visible to the current PostgreSQL role.", new NpgsqlConnectionStringBuilder(tasksDbConnectionString).Database);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "TasksDbContext migration failed. The application will continue running.");
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
    catch (Exception ex)
    {
        Console.Error.WriteLine($"DatabaseExistsAsync connection error: {ex.Message}");
        return false;
    }
}

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
