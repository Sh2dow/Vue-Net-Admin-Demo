using Microsoft.EntityFrameworkCore;

namespace backend.Domain.Data;

public class TasksDbContext : DbContext
{
    public TasksDbContext(DbContextOptions<TasksDbContext> options)
        : base(options)
    {
    }

    public DbSet<Models.TaskItem> Tasks => Set<Models.TaskItem>();
    public DbSet<Models.TaskComment> TaskComments => Set<Models.TaskComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.TaskItem>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(2000);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValue("todo");

            entity.Property(x => x.Priority)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValue("medium");

            entity.HasIndex(x => x.UserId);

            entity.HasMany(x => x.Comments)
                .WithOne(x => x.Task)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Models.TaskComment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Content)
                .IsRequired()
                .HasMaxLength(1000);

            entity.HasIndex(x => x.TaskId);
            entity.HasIndex(x => x.AuthorId);
        });
    }
}
