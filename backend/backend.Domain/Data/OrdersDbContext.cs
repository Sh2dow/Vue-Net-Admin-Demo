using Microsoft.EntityFrameworkCore;

namespace backend.Domain.Data;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
    {
    }

    public DbSet<Models.Order> Orders => Set<Models.Order>();
    public DbSet<Models.OrderSagaState> OrderSagaStates => Set<Models.OrderSagaState>();
    public DbSet<Models.OutboxMessage> OutboxMessages => Set<Models.OutboxMessage>();
    public DbSet<Models.ConsumedMessage> ConsumedMessages => Set<Models.ConsumedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.Order>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.UserId);

            entity.HasDiscriminator<string>("order_type")
                .HasValue<Models.DigitalOrder>("digital")
                .HasValue<Models.PhysicalOrder>("physical");
        });

        modelBuilder.Entity<Models.Order>().Ignore(x => x.Events);

        modelBuilder.Entity<Models.DigitalOrder>(entity =>
        {
            entity.Property(x => x.DownloadUrl)
                .IsRequired()
                .HasMaxLength(500);
        });

        modelBuilder.Entity<Models.PhysicalOrder>(entity =>
        {
            entity.Property(x => x.ShippingAddress)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(x => x.TrackingNumber)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<Models.OrderSagaState>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.State)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.ExecutionFailureReason)
                .HasMaxLength(1000);

            entity.HasIndex(x => x.OrderId)
                .IsUnique();
        });

        modelBuilder.Entity<Models.OutboxMessage>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.RoutingKey)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Payload)
                .IsRequired();

            entity.Property(x => x.CorrelationId)
                .HasMaxLength(100);

            entity.Property(x => x.LastError)
                .HasMaxLength(2000);

            entity.HasIndex(x => x.PublishedAtUtc);
            entity.HasIndex(x => x.OccurredAtUtc);
        });

        modelBuilder.Entity<Models.ConsumedMessage>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Consumer)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.MessageId)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => new { x.Consumer, x.MessageId })
                .IsUnique();
        });
    }
}
