using Microsoft.EntityFrameworkCore;

namespace backend.Domain.Data;

public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Models.PaymentEventRecord> PaymentEventRecords => Set<Models.PaymentEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.PaymentEventRecord>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Data)
                .IsRequired();

            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => new { x.OrderId, x.AttemptNumber, x.SequenceNumber })
                .IsUnique();
            entity.HasIndex(x => new { x.PaymentId, x.SequenceNumber })
                .IsUnique();
        });
    }
}
