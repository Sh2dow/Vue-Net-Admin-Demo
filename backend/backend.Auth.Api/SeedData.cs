using backend.Domain.Data;
using backend.Domain.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.EntityFrameworkCore.Models;
using System.Text.Json;

namespace backend.Auth.Api;

/// <summary>
/// Seeds the default admin user and the Vue.js frontend client application on startup.
/// Registered as a singleton hosted service — resolves scoped DbContext inside StartAsync.
/// </summary>
public sealed class SeedData : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeedData> _logger;

    public SeedData(IServiceProvider serviceProvider, ILogger<SeedData> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Resolve scoped DbContext inside a scope
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        await EnsureAdminUserAsync(dbContext, cancellationToken);
        await EnsureFrontendClientAsync(dbContext, cancellationToken);
        _logger.LogInformation("Seed data applied successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureAdminUserAsync(AuthDbContext dbContext, CancellationToken cancellationToken)
    {
        var admin = await dbContext.AppUsers
            .FirstOrDefaultAsync(u => u.Username == "admin", cancellationToken);

        if (admin is not null)
        {
            _logger.LogInformation("Admin user already exists, skipping.");
            return;
        }

        admin = new AppUser
        {
            Subject = Guid.NewGuid().ToString("N"),
            Username = "admin",
            Email = "admin@local.dev",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Roles = "admin"
        };

        dbContext.AppUsers.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Default admin user created (admin/Admin@123).");
    }

    private async Task EnsureFrontendClientAsync(AuthDbContext dbContext, CancellationToken cancellationToken)
    {
        var client = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ClientId == "vue-client", cancellationToken);

        if (client is not null)
        {
            _logger.LogInformation("Frontend client already exists, skipping.");
            return;
        }

        // OpenIddict 7.x: OpenIddictConstants removed — use string literals.
        // Permissions, RedirectUris, PostLogoutRedirectUris are JSON strings, not collections.
        client = new OpenIddictEntityFrameworkCoreApplication
        {
            ClientId = "vue-client",
            ClientType = "public",
            ConsentType = "explicit",
            DisplayName = "Vue.js Admin Dashboard",
            // Permissions: JSON array of permission strings
            Permissions = JsonSerializer.Serialize(new[]
            {
                // Endpoints
                "openiddict:permissions:endpoints:authorization",
                "openiddict:permissions:endpoints:token",
                "openiddict:permissions:endpoints:revocation",
                "openiddict:permissions:endpoints:introspection",
                "openiddict:permissions:endpoints:userinfo",
                // Grants
                "openiddict:permissions:grants:authorization_code",
                "openiddict:permissions:grants:refresh_token",
                // Scopes
                "openiddict:permissions:scopes:openid",
                "openiddict:permissions:scopes:profile",
                "openiddict:permissions:scopes:email",
                "openiddict:permissions:scopes:roles",
            }),
            // Redirect URIs: JSON array of URI strings
            RedirectUris = JsonSerializer.Serialize(new[]
            {
                "http://localhost:5173/callback",
                "http://localhost:5173/",
                "https://admin.example.com/callback",
                "https://admin.example.com/",
            }),
            // Post-logout redirect URIs: JSON array of URI strings
            PostLogoutRedirectUris = JsonSerializer.Serialize(new[]
            {
                "http://localhost:5173/",
                "https://admin.example.com/",
            }),
        };

        dbContext.Applications.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Frontend client 'vue-client' registered.");
    }
}
