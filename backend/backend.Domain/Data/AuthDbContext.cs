using backend.Domain.Models;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace backend.Domain.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // OpenIddict entities (v7.x naming)
    public DbSet<OpenIddictEntityFrameworkCoreApplication> Applications => Set<OpenIddictEntityFrameworkCoreApplication>();
    public DbSet<OpenIddictEntityFrameworkCoreAuthorization> Authorizations => Set<OpenIddictEntityFrameworkCoreAuthorization>();
    public DbSet<OpenIddictEntityFrameworkCoreToken> Tokens => Set<OpenIddictEntityFrameworkCoreToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Subject)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Username)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Email)
                .HasMaxLength(200);

            entity.Property(x => x.PasswordHash)
                .HasMaxLength(60);

            entity.Property(x => x.Roles)
                .HasMaxLength(500);

            entity.HasIndex(x => x.Subject)
                .IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        // Note: OpenIddict entity configuration is handled automatically
        // by UseOpenIddict() called in Auth.Api Program.cs
    }
}
