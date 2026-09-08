using Fleet.Modules.Drivers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Modules.Drivers.Persistence.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("certificates");

        builder.HasKey(certificate => certificate.Id);
        builder.Property(certificate => certificate.Id).ValueGeneratedNever();

        builder.Property(certificate => certificate.Number)
            .HasMaxLength(Certificate.NumberMaxLength)
            .IsRequired();

        builder.Property(certificate => certificate.Kind).IsRequired();

        // DateOnly maps to Postgres "date". No time, no zone, no daylight-saving argument about
        // whether a licence expired at midnight in Prague or in UTC.
        builder.Property(certificate => certificate.IssuedOn).IsRequired();
        builder.Property(certificate => certificate.ExpiresOn).IsRequired();

        builder.Property(certificate => certificate.ScanBlobId).HasMaxLength(200);
        builder.Property(certificate => certificate.SupersededAt);
        builder.Property(certificate => certificate.ExpiryWarningSentAt);

        // The expiry scan reads "current certificates expiring between these two dates" across the
        // whole table, which is precisely this index.
        builder.HasIndex(certificate => new { certificate.ExpiresOn, certificate.Kind });

        builder.HasIndex(certificate => new { certificate.DriverId, certificate.Kind });
    }
}
