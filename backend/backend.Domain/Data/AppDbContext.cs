using backend.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Domain.Data;

/// <summary>
/// Obsolete - kept for backward compatibility during migration.
/// Use TasksDbContext, OrdersDbContext, or PaymentsDbContext directly.
/// </summary>
[Obsolete("Use specific contexts: TasksDbContext, OrdersDbContext, or PaymentsDbContext")]
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Obsolete DbSets - these will be removed after migration
    [Obsolete("Use TasksDbContext.Tasks instead")]
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    
    [Obsolete("Use TasksDbContext.TaskComments instead")]
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    
    [Obsolete("Use OrdersDbContext.Orders instead")]
    public DbSet<Order> Orders => Set<Order>();
    
    [Obsolete("Use OrdersDbContext.OrderSagaStates instead")]
    public DbSet<OrderSagaState> OrderSagaStates => Set<OrderSagaState>();
    
    [Obsolete("Use OrdersDbContext.OutboxMessages instead")]
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    
    [Obsolete("Use OrdersDbContext.ConsumedMessages instead")]
    public DbSet<ConsumedMessage> ConsumedMessages => Set<ConsumedMessage>();
    
    [Obsolete("Use PaymentsDbContext.PaymentEventRecords instead")]
    public DbSet<PaymentEventRecord> PaymentEventRecords => Set<PaymentEventRecord>();
    
    [Obsolete("Use OrdersDbContext.Orders instead")]
    public DbSet<DigitalOrder> DigitalOrders => Set<DigitalOrder>();
    
    [Obsolete("Use OrdersDbContext.Orders instead")]
    public DbSet<PhysicalOrder> PhysicalOrders => Set<PhysicalOrder>();
    
    [Obsolete("Use OrdersDbContext.Orders instead")]
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Keep this empty now - all entity configuration moved to specific contexts
        // This prevents duplicate config errors while AppDbContext is still in use
        
        // Note: OrderEvent is not configured because it's an abstract base class for domain events
        // and EF Core doesn't support abstract base types without concrete derived types.
        
        // Configure AppUser (for auth-related user data)
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Subject).IsUnique();
        });
        
        // Configure Order navigation to avoid relationship errors with Events property
        modelBuilder.Entity<Order>().Ignore(x => x.Events);
    }
}
