using backend.Domain.Data;
using backend.Domain.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

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
        try
        {
            // Resolve scoped services inside a scope
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            // Apply pending migrations before seeding (idempotent — no-op if already applied)
            _logger.LogInformation("Applying pending AuthDbContext migrations...");
            await dbContext.Database.MigrateAsync(cancellationToken);

            await EnsureAdminUserAsync(dbContext, cancellationToken);
            await EnsureFrontendClientAsync(appManager, cancellationToken);
            _logger.LogInformation("Seed data applied successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SeedData failed to apply migrations or seed data. The application will continue running, but seeding must be completed manually (e.g., via 'dotnet ef database update'). Common cause: the target PostgreSQL database does not exist yet.");
        }
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

    private async Task EnsureFrontendClientAsync(IOpenIddictApplicationManager manager, CancellationToken cancellationToken)
    {
        // Delete existing client first to ensure clean state
        var existing = await manager.FindByClientIdAsync("vue-client", cancellationToken);
        if (existing is not null)
        {
            await manager.DeleteAsync(existing, cancellationToken);
            _logger.LogInformation("Frontend client 'vue-client' deleted (recreating with updated permissions).");
        }

        // Use management API to create client with correct OpenIddict 7.x abbreviated permission keys
        // ept: = endpoint, gt: = grant type, scp: = scope
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "vue-client",
            ClientType = "public",
            ConsentType = "implicit",
            DisplayName = "Vue.js Admin Dashboard",
            Permissions =
            {
                // Endpoint permissions (abbreviated format)
                "ept:authorization",
                "ept:token",
                "ept:revocation",
                "ept:introspection",
                "ept:end_session",
                // Grant type permissions (abbreviated format)
                "gt:authorization_code",
                "gt:refresh_token",
                // Response type permissions (prefix: rst:)
                "rst:code",
                // Scope permissions (abbreviated format)
                "scp:openid",
                "scp:profile",
                "scp:email",
                "scp:roles",
                "scp:offline_access",
            },
            RedirectUris =
            {
                new Uri("http://localhost:5173/login"),
            },
            PostLogoutRedirectUris =
            {
                new Uri("http://localhost:5173/"),
                new Uri("http://localhost:5173/login"),
            },
        };

        await manager.CreateAsync(descriptor, cancellationToken);
        _logger.LogInformation("Frontend client 'vue-client' registered.");
    }
}
