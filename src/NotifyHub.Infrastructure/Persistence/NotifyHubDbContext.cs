using Microsoft.EntityFrameworkCore;
using NotifyHub.Core.Entities;

namespace NotifyHub.Infrastructure.Persistence;

public sealed class NotifyHubDbContext(DbContextOptions<NotifyHubDbContext> options)
    : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotifyHubDbContext).Assembly);
    }
}
