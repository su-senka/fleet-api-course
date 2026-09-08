using Fleet.Modules.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Vehicles.Persistence.Configurations;

internal sealed class OdometerReadingConfiguration : IEntityTypeConfiguration<OdometerReading>
{
    public void Configure(EntityTypeBuilder<OdometerReading> builder)
    {
        builder.ToTable("odometer_readings");

        builder.HasKey(reading => reading.Id);
        builder.Property(reading => reading.Id).ValueGeneratedNever();

        builder.Property(reading => reading.RecordedAt).IsRequired();
        builder.Property(reading => reading.Km).IsRequired();

        // The history is always read as "this vehicle, newest first". A composite index in that
        // exact order lets Postgres answer the page without sorting 15,000 rows.
        builder.HasIndex(reading => new { reading.VehicleId, reading.RecordedAt })
            .IsDescending(false, true);
    }
}
