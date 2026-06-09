using backend.Domain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace backend.Domain.Design;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Auth");
        if (!string.IsNullOrWhiteSpace(envConnectionString))
        {
            return CreateDbContext(envConnectionString);
        }

        var basePath = ResolveConfigurationBasePath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Auth");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'Auth' was not found. Configuration base path: '{basePath}'.");
        }

        return CreateDbContext(connectionString);
    }

    private static string ResolveConfigurationBasePath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        var candidates = new[]
        {
            current.FullName,
            Path.Combine(current.FullName, "backend.Auth.Api"),
            current.Parent?.FullName,
            current.Parent is null ? null : Path.Combine(current.Parent.FullName, "backend.Auth.Api")
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => path!)
        .Distinct()
        .ToArray();

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate appsettings.json. Checked: {string.Join(", ", candidates)}");
    }

    private static AuthDbContext CreateDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AuthDbContext).Assembly.GetName().Name))
            .UseOpenIddict();

        return new AuthDbContext(optionsBuilder.Options);
    }
}
