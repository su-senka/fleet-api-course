using Fleet.Modules.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Vehicles.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(vehicle => vehicle.Id);

        // Keys are created in application code - Guid.CreateVersion7 for real rows,
        // DeterministicGuid for seed data - never by the database. Saying so matters: EF Core
        // otherwise assumes a Guid key it did not generate belongs to a row that already exists,
        // and issues an UPDATE where an INSERT was meant.
        builder.Property(vehicle => vehicle.Id).ValueGeneratedNever();

        builder.Property(vehicle => vehicle.Plate)
            .HasMaxLength(Vehicle.PlateMaxLength)
            .IsRequired();

        // One plate, one vehicle. The service checks for a duplicate first so it can return a
        // tidy Conflict, but the index is what actually guarantees it under concurrency.
        builder.HasIndex(vehicle => vehicle.Plate).IsUnique();

        // Enums are stored as their integer values. Storing them as text reads better in psql and
        // survives reordering, but costs a conversion on every query; either choice is defensible,
        // and this one is the EF Core default.
        builder.Property(vehicle => vehicle.Type).IsRequired();
        builder.Property(vehicle => vehicle.Status).IsRequired();

        builder.Property(vehicle => vehicle.OdometerKm).IsRequired();
        builder.Property(vehicle => vehicle.LastReadingAt);

        builder.HasOne(vehicle => vehicle.Depot)
            .WithMany()
            .HasForeignKey(vehicle => vehicle.DepotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(vehicle => vehicle.OdometerReadings)
            .WithOne()
            .HasForeignKey(reading => reading.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(vehicle => vehicle.OdometerReadings)
            .HasField("_odometerReadings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Listing by depot and filtering by status are the two things every screen does.
        builder.HasIndex(vehicle => vehicle.DepotId);
        builder.HasIndex(vehicle => vehicle.Status);
    }
}
