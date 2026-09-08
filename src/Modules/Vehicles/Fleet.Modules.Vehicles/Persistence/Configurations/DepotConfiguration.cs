using Fleet.Modules.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Vehicles.Persistence.Configurations;

internal sealed class DepotConfiguration : IEntityTypeConfiguration<Depot>
{
    public void Configure(EntityTypeBuilder<Depot> builder)
    {
        builder.ToTable("depots");

        builder.HasKey(depot => depot.Id);
        builder.Property(depot => depot.Id).ValueGeneratedNever();

        builder.Property(depot => depot.Name).HasMaxLength(120).IsRequired();
        builder.Property(depot => depot.City).HasMaxLength(80).IsRequired();

        builder.HasIndex(depot => depot.Name).IsUnique();
    }
}
