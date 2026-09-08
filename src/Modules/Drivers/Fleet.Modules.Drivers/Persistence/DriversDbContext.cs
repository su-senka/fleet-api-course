using Fleet.Common.Messaging.Outbox;
using Fleet.Common.Persistence;
using Fleet.Modules.Drivers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Drivers.Persistence;

/// <summary>The Drivers module's own <c>DbContext</c>, mapped entirely inside the <c>drivers</c> schema.</summary>
internal sealed class DriversDbContext(DbContextOptions<DriversDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "drivers";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Certificate> Certificates => Set<Certificate>();

    /// <summary>
    /// This module's outbox, in this module's schema.
    /// </summary>
    /// <remarks>
    /// It is here, rather than in some shared table, so that an event and the state change that
    /// caused it commit together. <c>SaveChangesAsync</c> on this context writes both or neither.
    /// </remarks>
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DriversDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
