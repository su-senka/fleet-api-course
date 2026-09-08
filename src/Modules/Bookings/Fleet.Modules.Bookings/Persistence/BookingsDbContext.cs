using Fleet.Common.Persistence;
using Fleet.Modules.Bookings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Bookings.Persistence;

/// <summary>The Bookings module's own <c>DbContext</c>, mapped entirely inside the <c>bookings</c> schema.</summary>
internal sealed class BookingsDbContext(DbContextOptions<BookingsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "bookings";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// The exclusion constraint that stops one vehicle being booked twice. Named here because the
    /// service catches the violation by name-free error code, but a human reading a Postgres log
    /// deserves to find it.
    /// </summary>
    public const string NoOverlapConstraintName = "ex_bookings_no_overlapping_windows";

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingsDbContext).Assembly);
    }
}
