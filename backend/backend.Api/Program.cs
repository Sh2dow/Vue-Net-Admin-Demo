using backend.Api;
using backend.Api.Application.Exceptions;
using backend.Api.Controllers;
using backend.Infrastructure.Application.Behaviors;
using backend.Infrastructure.Application.Users;
using backend.Infrastructure.Infrastructure.Messaging;
using backend.ServiceDefaults;
using backend.Shared.Application.Users;
using backend.Shared.Configuration;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RabbitMqOptions = backend.Shared.Configuration.RabbitMqOptions;

var builder = WebApplication.CreateBuilder(args);
var featureAssemblies = new[]
{
    typeof(Program).Assembly
};

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(featureAssemblies)
);
builder.Services.AddValidatorsFromAssemblies(featureAssemblies);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IEffectiveUserAccessor, EffectiveUserAccessor>();

// Configure strongly-typed options from configuration
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.Configure<AuthServiceOptions>(builder.Configuration.GetSection(AuthServiceOptions.SectionName));
builder.Services.Configure<DownstreamServicesOptions>(builder.Configuration.GetSection(DownstreamServicesOptions.SectionName));
builder.Services.AddHttpClient("Orders", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
    ConfigureDownstreamClient(client, options.OrdersBaseUrl, $"{DownstreamServicesOptions.SectionName}:OrdersBaseUrl");
});
builder.Services.AddHttpClient("Payments", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
    ConfigureDownstreamClient(client, options.PaymentsBaseUrl, $"{DownstreamServicesOptions.SectionName}:PaymentsBaseUrl");
});
builder.Services.AddHttpClient("Users", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
    ConfigureDownstreamClient(client, options.UsersBaseUrl, $"{DownstreamServicesOptions.SectionName}:UsersBaseUrl");
});
builder.Services.AddHttpClient("Tasks", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
    ConfigureDownstreamClient(client, options.TasksBaseUrl, $"{DownstreamServicesOptions.SectionName}:TasksBaseUrl");
});
builder.Services.AddHttpClient<IUserDirectory, HttpUserDirectory>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AuthServiceOptions>>().Value;
    ConfigureDownstreamClient(client, options.BaseUrl, $"{AuthServiceOptions.SectionName}:BaseUrl");
});

// Register integration event outbox - removed since Users handlers are no longer registered in main API
// builder.Services.AddTransient<IIntegrationEventOutbox, DbIntegrationEventOutbox>();

// Note: Shared DbContext removed - each service (Tasks, Orders, Payments, Auth) now has its own DB
// The main API is now a gateway/BFF that routes to individual services via HTTP or reverse proxy

// Configure RabbitMQ connection factory with environment variable support
builder.Services.AddSingleton<RabbitMqConnectionFactory>();

// Override RabbitMQ URI from environment variable if available
builder.Services.PostConfigure<RabbitMqOptions>(options =>
{
    var aspireConnectionString = builder.Configuration.GetConnectionString("messaging");
    if (!string.IsNullOrWhiteSpace(aspireConnectionString))
    {
        options.Uri = aspireConnectionString;
    }
});

// Note: RabbitMqOutboxDispatcher removed from backend.Api (gateway/BFF)
// Each service (Orders, Tasks, Payments, Auth) should register its own dispatcher if needed

// CORS — origins from configuration (override via env var: CORS__AllowedOrigins)
// Supports comma-separated string (shell-friendly) or JSON array
builder.Services.AddCors(options =>
{
    options.AddPolicy("cors", p =>
    {
        var corsBuilder = p.AllowAnyHeader().AllowAnyMethod();
        var raw = builder.Configuration.GetValue<string>("CORS:AllowedOrigins");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var origins = raw.StartsWith("[")
                ? raw.Trim('[', ']', '"', '\'').Split(',').Select(s => s.Trim('"', '\'')).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
                : raw.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (origins.Count > 0)
                corsBuilder.WithOrigins(origins.ToArray());
            else
                corsBuilder.WithOrigins("http://localhost:5173");
        }
        else
        {
            corsBuilder.WithOrigins("http://localhost:5173");
        }
    });
});

// Configure authentication against OpenIddict authority
var authAuthority = builder.Configuration["Auth:Authority"];
if (string.IsNullOrWhiteSpace(authAuthority))
{
    throw new InvalidOperationException(
        "Auth authority is missing. Configure 'Auth:Authority'in appsettings.json " +
        "or provide it via environment variables.");
}

builder.Services.AddJwtBearerAuthentication(authAuthority);

builder.Services.AddAuthorization();

var app = builder.Build();

var rabbitMqOptions = app.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>();

string FormatRabbitMqTarget(string? uriString)
{
    if (string.IsNullOrWhiteSpace(uriString))
    {
        return "missing";
    }

    if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
    {
        return "invalid";
    }

    return $"{uri.Host}:{uri.Port}";
}

static void ConfigureDownstreamClient(HttpClient client, string? baseUrl, string settingName)
{
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException(
            $"{settingName} is missing. Configure it in appsettings.json or provide it via environment variables.");
    }

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}

app.Logger.LogInformation(
    "Startup config. Environment={Environment}; RabbitMq={RabbitMq}; AuthAuthority={AuthAuthority}",
    app.Environment.EnvironmentName,
    FormatRabbitMqTarget(rabbitMqOptions?.Uri),
    authAuthority);

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseRouting();
app.UseCors("cors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
