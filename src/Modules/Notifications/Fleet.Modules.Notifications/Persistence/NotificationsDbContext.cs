using Fleet.Common.Persistence;
using Fleet.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Notifications.Persistence;

/// <summary>The Notifications module's own <c>DbContext</c>, in the <c>notifications</c> schema.</summary>
/// <remarks>
/// No outbox here. This module only ever consumes events; it announces nothing.
/// </remarks>
internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "notifications";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
