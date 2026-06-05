using System.Net;
using backend.Domain.Data;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Database;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.ServiceDefaults;
using backend.Shared.Application.Messaging;
using backend.Shared.Application.Users;
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
builder.Services.Configure<backend.Shared.Configuration.RabbitMqOptions>(
    builder.Configuration.GetSection(backend.Shared.Configuration.RabbitMqOptions.SectionName));

// Register outbox for tasks service
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox<TasksDbContext>>();
if (builder.Configuration.GetValue<bool>("RabbitMq:Enabled", false))
{
    builder.Services.AddHostedService<OutboxDispatcher<TasksDbContext>>();
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("default", opt =>
    {
        opt.PermitLimit = 50;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

builder.Services.AddDatabaseMigration<TasksDbContext>();

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

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "tasks-api" }));

app.Run();
