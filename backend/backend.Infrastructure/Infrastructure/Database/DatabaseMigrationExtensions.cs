using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Infrastructure.Infrastructure.Database;

public static class DatabaseMigrationExtensions
{
    public static IServiceCollection AddDatabaseMigration<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddSingleton<DatabaseMigrationService<TDbContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<DatabaseMigrationService<TDbContext>>());
        return services;
    }
}
