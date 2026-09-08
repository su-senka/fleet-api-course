using Fleet.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Reporting.Persistence.Configurations;

internal sealed class ReportJobConfiguration : IEntityTypeConfiguration<ReportJob>
{
    public void Configure(EntityTypeBuilder<ReportJob> builder)
    {
        builder.ToTable("report_jobs");

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).ValueGeneratedNever();

        builder.Property(job => job.Kind).IsRequired();
        builder.Property(job => job.ParametersJson).HasColumnType("jsonb").IsRequired();
        builder.Property(job => job.Status).IsRequired();

        builder.Property(job => job.ResultBlobId).HasMaxLength(ReportJob.BlobIdMaxLength);
        builder.Property(job => job.FailureReason).HasMaxLength(ReportJob.FailureReasonMaxLength);

        builder.Property(job => job.CreatedAt).IsRequired();
        builder.Property(job => job.StartedAt);
        builder.Property(job => job.CompletedAt);

        // The background service asks "what is queued, oldest first?" on every tick. Same partial
        // index trick as the outbox, for the same reason: completed jobs are the ones that pile up.
        builder.HasIndex(job => job.CreatedAt)
            .HasFilter("status = 1")
            .HasDatabaseName("ix_report_jobs_queued");
    }
}
