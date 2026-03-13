using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<FishingSpot> FishingSpots { get; set; }
    public DbSet<Catch> Catches { get; set; }
    public DbSet<VideoAnalysis> VideoAnalyses { get; set; }
    public DbSet<FishingSession> FishingSessions { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Pontoon> Pontoons { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations from the same assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
