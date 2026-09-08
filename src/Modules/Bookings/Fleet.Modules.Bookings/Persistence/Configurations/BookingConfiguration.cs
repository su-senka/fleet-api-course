using Fleet.Modules.Bookings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Bookings.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).ValueGeneratedNever();

        builder.Property(booking => booking.Purpose)
            .HasMaxLength(Booking.PurposeMaxLength)
            .IsRequired();

        builder.Property(booking => booking.StartsAt).IsRequired();
        builder.Property(booking => booking.EndsAt).IsRequired();
        builder.Property(booking => booking.Status).IsRequired();
        builder.Property(booking => booking.CreatedAt).IsRequired();
        builder.Property(booking => booking.CancelledAt);

        // VehicleId and DriverId are plain columns with no foreign key. They point into other
        // modules' schemas, and a cross-schema FK is exactly the coupling the module boundary
        // exists to prevent. They are indexed because every list query filters on one or the other.
        builder.Property(booking => booking.VehicleId).IsRequired();
        builder.Property(booking => booking.DriverId).IsRequired();

        builder.HasIndex(booking => new { booking.VehicleId, booking.StartsAt });
        builder.HasIndex(booking => new { booking.DriverId, booking.StartsAt });
        builder.HasIndex(booking => booking.StartsAt);

        // xmin is a system column every Postgres row already has: the transaction id that last
        // wrote it. Mapping it as a concurrency token gives optimistic concurrency for free - no
        // extra column, no trigger, nothing for application code to remember to increment.
        //
        // ValueGeneratedOnAddOrUpdate tells EF the database owns the value, so it is read back
        // after every write and never sent in one.
        builder.Property(booking => booking.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
