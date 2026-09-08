using Fleet.Common.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Common.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecordEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecordEntity> builder)
    {
        builder.ToTable("idempotency_keys");

        // The client's key *is* the primary key. That is what makes claiming one atomic: two
        // concurrent inserts of the same key cannot both succeed, because the database will not
        // allow it. A check-then-insert would lose that race, and losing it means charging
        // somebody twice.
        builder.HasKey(record => record.Key);

        builder.Property(record => record.Key)
            .HasMaxLength(IdempotencyRecordEntity.KeyMaxLength)
            .ValueGeneratedNever();

        builder.Property(record => record.RequestFingerprint)
            .HasMaxLength(IdempotencyRecordEntity.FingerprintMaxLength)
            .IsRequired();

        builder.Property(record => record.State).IsRequired();
        builder.Property(record => record.StatusCode);
        builder.Property(record => record.ResponseBody);
        builder.Property(record => record.CreatedAt).IsRequired();
        builder.Property(record => record.CompletedAt);

        // The purge job deletes by age.
        builder.HasIndex(record => record.CreatedAt);
    }
}
