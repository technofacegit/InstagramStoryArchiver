using InstagramStoryArchiver.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstagramStoryArchiver.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<MonitoredInstagramUser> MonitoredUsers => Set<MonitoredInstagramUser>();
    public DbSet<ArchivedInstagramStory> ArchivedStories => Set<ArchivedInstagramStory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
