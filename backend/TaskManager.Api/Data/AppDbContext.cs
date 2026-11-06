using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<BackgroundJob> Jobs => Set<BackgroundJob>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<User>();
        user.HasIndex(x => x.Username).IsUnique();
        user.Property(x => x.Username).HasMaxLength(150);
        user.Property(x => x.PasswordHash).HasMaxLength(256);
        user.Property(x => x.FirstName).HasMaxLength(150);
        user.Property(x => x.LastName).HasMaxLength(150);
        user.Property(x => x.Email).HasMaxLength(254);
        user.Property(x => x.Role).HasMaxLength(50);
        user.Property(x => x.Phone).HasMaxLength(20);
        user.Property(x => x.AvatarPicture).HasMaxLength(100);

        var tag = model.Entity<Tag>();
        tag.Property(x => x.Title).HasMaxLength(100);
        tag.HasIndex(x => x.UniqueId).IsUnique();

        var task = model.Entity<WorkTask>();
        task.Property(x => x.Title).HasMaxLength(100);
        task.Property(x => x.Condition).HasMaxLength(50);
        task.ToTable(t => t.HasCheckConstraint("CK_Tasks_Priority", "\"Priority\" >= 0"));
        task.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.SetNull);
        task.HasOne(x => x.Executor).WithMany().HasForeignKey(x => x.ExecutorId).OnDelete(DeleteBehavior.SetNull);
        task.HasMany(x => x.Tags).WithMany(x => x.Tasks).UsingEntity("TaskTags");

        var job = model.Entity<BackgroundJob>();
        job.Property(x => x.Kind).HasMaxLength(50);
        job.Property(x => x.Status).HasMaxLength(20);
        job.HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var entry in ChangeTracker.Entries<WorkTask>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.DateCreated = today;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.DateUpdated = today;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
