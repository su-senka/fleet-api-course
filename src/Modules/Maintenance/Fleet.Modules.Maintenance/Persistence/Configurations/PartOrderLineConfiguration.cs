using Fleet.Modules.Maintenance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Maintenance.Persistence.Configurations;

internal sealed class PartOrderLineConfiguration : IEntityTypeConfiguration<PartOrderLine>
{
    public void Configure(EntityTypeBuilder<PartOrderLine> builder)
    {
        builder.ToTable("part_order_lines");

        // A composite key, not a surrogate id. "One line per part per work order" is a rule, and
        // this is the cheapest possible place to enforce it.
        builder.HasKey(line => new { line.WorkOrderId, line.PartNumber });

        builder.Property(line => line.PartNumber)
            .HasMaxLength(PartOrderLine.PartNumberMaxLength)
            .IsRequired();

        builder.Property(line => line.Quantity).IsRequired();

        // numeric(12,2), never a floating-point type. Money in a double is how you end up billing
        // somebody 1899.9999999999998 crowns.
        builder.Property(line => line.UnitPrice)
            .HasColumnType("numeric(12,2)")
            .IsRequired();
    }
}
