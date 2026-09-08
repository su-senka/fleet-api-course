using Fleet.Modules.Drivers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Drivers.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(driver => driver.Id);

        // See VehicleConfiguration: the application supplies every key, so EF must not treat a
        // populated Guid as evidence that the row is already in the database.
        builder.Property(driver => driver.Id).ValueGeneratedNever();

        builder.Property(driver => driver.EmployeeNumber)
            .HasMaxLength(Driver.EmployeeNumberMaxLength)
            .IsRequired();

        builder.Property(driver => driver.Name)
            .HasMaxLength(Driver.NameMaxLength)
            .IsRequired();

        builder.Property(driver => driver.UserId).HasMaxLength(200);

        builder.HasIndex(driver => driver.EmployeeNumber).IsUnique();

        // Every authorized request from a driver resolves their token subject to a row through
        // this index, so it is worth having even though only six of the sixty rows use it.
        // Filtered, because "unique" must not mean "at most one driver without a login".
        builder.HasIndex(driver => driver.UserId)
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");

        builder.HasMany(driver => driver.Certificates)
            .WithOne()
            .HasForeignKey(certificate => certificate.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(driver => driver.Certificates)
            .HasField("_certificates")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
