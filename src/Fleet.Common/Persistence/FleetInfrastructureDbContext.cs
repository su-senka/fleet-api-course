using Fleet.Common.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Common.Persistence;

/// <summary>
/// The one table that belongs to no module: the idempotency key store.
/// </summary>
/// <remarks>
/// It lives in a <c>shared</c> schema rather than in any module's, because idempotency is a
/// property of an HTTP request rather than of vehicles or bookings. Putting it in, say, the
/// bookings schema would make it look like Bookings owned it, and the middleware that uses it
/// applies to whatever endpoints you decide to put it on.
/// </remarks>
internal sealed class FleetInfrastructureDbContext(DbContextOptions<FleetInfrastructureDbContext> options)
    : DbContext(options)
{
    public const string Schema = "shared";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<IdempotencyRecordEntity> IdempotencyKeys => Set<IdempotencyRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new Configurations.IdempotencyRecordConfiguration());
    }
}
