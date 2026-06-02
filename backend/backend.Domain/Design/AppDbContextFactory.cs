using backend.Domain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace backend.Domain.Design;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
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

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'Default' was not found. Configuration base path: '{basePath}'.");
        }

        return CreateDbContext(connectionString);
    }

    private static string ResolveConfigurationBasePath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        var candidates = new[]
        {
            current.FullName,
            Path.Combine(current.FullName, "backend.Api"),
            current.Parent?.FullName,
            current.Parent is null ? null : Path.Combine(current.Parent.FullName, "backend.Api")
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

    private static AppDbContext CreateDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}
