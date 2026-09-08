using Fleet.Common.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Common.Tests;

/// <summary>
/// A stand-in for a module's <c>DbContext</c>, with nothing in it but an outbox.
/// </summary>
/// <remarks>
/// The outbox base types are generic over the module's own context, so testing them needs a
/// context - but not a module. This one maps exactly what every publishing module maps, and
/// nothing else, so a failure here is a failure in the base types rather than in Drivers.
/// </remarks>
public sealed class TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : DbContext(options)
{
    public const string Schema = "outbox_tests";

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
