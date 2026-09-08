using Fleet.Modules.Maintenance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Maintenance.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");

        builder.HasKey(workOrder => workOrder.Id);
        builder.Property(workOrder => workOrder.Id).ValueGeneratedNever();

        builder.Property(workOrder => workOrder.VehicleId).IsRequired();
        builder.Property(workOrder => workOrder.Kind).IsRequired();
        builder.Property(workOrder => workOrder.Status).IsRequired();
        builder.Property(workOrder => workOrder.OpenedAt).IsRequired();
        builder.Property(workOrder => workOrder.ClosedAt);

        builder.Property(workOrder => workOrder.SupplierOrderId)
            .HasMaxLength(WorkOrder.SupplierOrderIdMaxLength);

        builder.HasMany(workOrder => workOrder.Lines)
            .WithOne()
            .HasForeignKey(line => line.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(workOrder => workOrder.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(workOrder => new { workOrder.VehicleId, workOrder.OpenedAt });
        builder.HasIndex(workOrder => workOrder.Status);
    }
}
